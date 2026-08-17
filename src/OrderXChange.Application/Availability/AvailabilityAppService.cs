using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Authorization;
using OrderXChange.Domain.Staging;
using OrderXChange.Permissions;
using OrderXChange.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Volo.Abp.TenantManagement.Talabat;
using Volo.Abp.Timing;

namespace OrderXChange.Availability;

[Authorize(OrderXChangePermissions.Availability.Default)]
public class AvailabilityAppService : ApplicationService, IAvailabilityAppService
{
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepo;
    private readonly IRepository<ItemAvailabilityState, Guid> _availabilityRepo;
    private readonly IRepository<TalabatAccount, Guid> _talabatAccountRepo;
    private readonly ICurrentUserBranchProvider _branchProvider;
    private readonly IClock _clock;
    private readonly IDataFilter _dataFilter;
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly TalabatAvailabilityPushService _talabatPush;

    private const string DefaultDayEndTime = "05:00";
    private const string DefaultTimeZone = "Asia/Kuwait";

    public AvailabilityAppService(
        IRepository<FoodicsProductStaging, Guid> stagingRepo,
        IRepository<ItemAvailabilityState, Guid> availabilityRepo,
        IRepository<TalabatAccount, Guid> talabatAccountRepo,
        ICurrentUserBranchProvider branchProvider,
        IClock clock,
        IDataFilter dataFilter,
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        TalabatAvailabilityPushService talabatPush)
    {
        _stagingRepo = stagingRepo;
        _availabilityRepo = availabilityRepo;
        _talabatAccountRepo = talabatAccountRepo;
        _branchProvider = branchProvider;
        _clock = clock;
        _dataFilter = dataFilter;
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _talabatPush = talabatPush;
    }

    public async Task<PagedResultDto<AvailabilityItemDto>> GetItemsAsync(GetAvailabilityInput input)
    {
        // Vendors (branches) come from TalabatAccount. Show every product across all the
        // caller's accessible branches, each item carrying its per-branch stock state.
        var vendors = await ResolveVendorsAsync();
        if (vendors.Count == 0)
            return new PagedResultDto<AvailabilityItemDto>(0, new List<AvailabilityItemDto>());

        var accountIds = vendors.Select(v => v.AccountId).Distinct().ToList();
        var vendorCodes = vendors.Select(v => v.Code).ToList();

        var query = (await _stagingRepo.GetQueryableAsync()).AsNoTracking()
            .Where(x => !x.IsDeleted && accountIds.Contains(x.FoodicsAccountId));

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var s = input.Search.Trim();
            query = query.Where(x => x.Name.Contains(s) || (x.Sku != null && x.Sku.Contains(s)) || x.FoodicsProductId.Contains(s));
        }

        var totalCount = await query.CountAsync();
        var rows = await query
            .OrderBy(x => x.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .Select(x => new { x.FoodicsProductId, x.FoodicsAccountId, x.Name, x.NameLocalized, x.Sku, x.CategoryName })
            .ToListAsync();

        var productIds = rows.Select(r => r.FoodicsProductId).ToList();
        var stateQuery = await _availabilityRepo.GetQueryableAsync();
        var outStates = await stateQuery
            .Where(a => !a.IsInStock && vendorCodes.Contains(a.VendorCode) && productIds.Contains(a.FoodicsProductId))
            .Select(a => new { a.FoodicsProductId, a.VendorCode, a.Mode, a.RestoreAtUtc })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var branches = vendors
                .Where(v => v.AccountId == r.FoodicsAccountId)
                .Select(v =>
                {
                    var st = outStates.FirstOrDefault(s => s.FoodicsProductId == r.FoodicsProductId && s.VendorCode == v.Code);
                    return new AvailabilityBranchStateDto
                    {
                        VendorCode = v.Code,
                        BranchName = v.DisplayName,
                        IsInStock = st == null,
                        Mode = st?.Mode,
                        RestoreAtUtc = st?.RestoreAtUtc,
                    };
                })
                .OrderBy(b => b.BranchName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AvailabilityItemDto
            {
                FoodicsProductId = r.FoodicsProductId,
                Name = r.Name,
                NameLocalized = r.NameLocalized,
                Sku = r.Sku,
                CategoryName = r.CategoryName,
                Branches = branches,
                BranchCount = branches.Count,
                OutOfStockCount = branches.Count(b => !b.IsInStock),
            };
        }).ToList();

        return new PagedResultDto<AvailabilityItemDto>(totalCount, items);
    }

    [Authorize(OrderXChangePermissions.Availability.Manage)]
    public async Task SetAvailabilityAsync(SetAvailabilityInput input)
    {
        if (input.FoodicsProductIds.Count == 0 || input.VendorCodes.Count == 0)
            throw new UserFriendlyException("Select at least one item and one branch.");

        var vendors = await ResolveVendorsAsync();
        var targets = vendors.Where(v => input.VendorCodes.Contains(v.Code)).ToList();
        if (targets.Count == 0)
            return; // fail-closed: nothing in the caller's branch scope

        var mode = input.Mode == AvailabilityMode.ForADay ? AvailabilityMode.ForADay : AvailabilityMode.TillFurtherNotice;
        DateTime? restoreAt = mode == AvailabilityMode.ForADay ? await ResolveForADayRestoreUtcAsync() : null;

        foreach (var vendor in targets)
        {
            foreach (var productId in input.FoodicsProductIds.Distinct())
            {
                // The unique index (Tenant, Account, Product, Vendor) spans soft-deleted
                // rows, so a stale soft-deleted state collides on insert (was a 500 on
                // the second out-of-stock after an in-stock toggle). Look it up with the
                // soft-delete filter OFF and revive/hard-delete instead of re-inserting.
                ItemAvailabilityState? existing;
                using (_dataFilter.Disable<ISoftDelete>())
                {
                    existing = await _availabilityRepo.FirstOrDefaultAsync(
                        a => a.FoodicsProductId == productId && a.VendorCode == vendor.Code);
                }

                if (input.InStock)
                {
                    if (existing != null)
                        await _availabilityRepo.HardDeleteAsync(existing);
                }
                else if (existing == null)
                {
                    await _availabilityRepo.InsertAsync(new ItemAvailabilityState
                    {
                        FoodicsAccountId = vendor.AccountId,
                        FoodicsProductId = productId,
                        VendorCode = vendor.Code,
                        IsInStock = false,
                        Mode = mode,
                        RestoreAtUtc = restoreAt,
                    });
                }
                else
                {
                    existing.IsDeleted = false; // revive if it was previously soft-deleted
                    existing.IsInStock = false;
                    existing.Mode = mode;
                    existing.RestoreAtUtc = restoreAt;
                    await _availabilityRepo.UpdateAsync(existing);
                }
            }
        }

        // §5: push the change to Talabat so the item actually goes (un)available there.
        // Best-effort — a push failure must not fail the local toggle. For "for a day" we also
        // send availableAt so Talabat can auto-restore; a manual "mark in stock" pushes available=true.
        foreach (var vendor in targets)
        {
            try
            {
                await _talabatPush.PushAsync(
                    vendor.AccountId,
                    vendor.BranchId,
                    vendor.Code,
                    vendor.ChainCode,
                    vendor.PosVendorId,
                    input.FoodicsProductIds,
                    input.InStock,
                    input.InStock ? null : restoreAt);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Talabat availability push failed for vendor {VendorCode}.", vendor.Code);
            }
        }
    }

    private async Task<List<VendorInfo>> ResolveVendorsAsync()
    {
        var scope = await _branchProvider.GetScopeAsync();
        var query = (await _talabatAccountRepo.GetQueryableAsync()).Where(a => a.IsActive);
        if (!scope.AllBranches)
        {
            if (scope.BranchIds.Count == 0)
                return new List<VendorInfo>();
            query = query.Where(a => a.FoodicsBranchId != null && scope.BranchIds.Contains(a.FoodicsBranchId));
        }

        var list = await query
            .Where(a => a.FoodicsAccountId != null)
            .Select(a => new
            {
                a.VendorCode,
                a.FoodicsAccountId,
                a.FoodicsBranchName,
                a.Name,
                a.ChainCode,
                a.PlatformRestaurantId,
                a.FoodicsBranchId
            })
            .Distinct()
            .ToListAsync();

        return list
            .Select(x => new VendorInfo(
                x.VendorCode,
                x.FoodicsAccountId!.Value,
                !string.IsNullOrWhiteSpace(x.FoodicsBranchName)
                    ? x.FoodicsBranchName!
                    : (!string.IsNullOrWhiteSpace(x.Name) ? x.Name : x.VendorCode),
                x.ChainCode,
                x.PlatformRestaurantId,
                x.FoodicsBranchId))
            .ToList();
    }

    private sealed record VendorInfo(
        string Code, Guid AccountId, string DisplayName,
        string? ChainCode, string? PosVendorId, string? BranchId);

    public async Task<AvailabilitySettingsDto> GetSettingsAsync()
    {
        var (time, tzId) = await ReadScheduleSettingsAsync();
        return new AvailabilitySettingsDto
        {
            DayEndTime = time,
            TimeZone = tzId,
            NextRestoreAtUtc = ComputeNextRestoreUtc(time, tzId)
        };
    }

    [Authorize(OrderXChangePermissions.Availability.Manage)]
    public async Task<AvailabilitySettingsDto> UpdateSettingsAsync(UpdateAvailabilitySettingsInput input)
    {
        var time = (input.DayEndTime ?? string.Empty).Trim();
        if (!TryParseTimeOfDay(time, out _, out _))
            throw new UserFriendlyException("Enter the day-end time as HH:mm (e.g. 05:00).");

        var tzId = (input.TimeZone ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tzId) || !TryResolveTimeZone(tzId, out _))
            throw new UserFriendlyException($"Unknown timezone '{tzId}'. Use an id like 'Asia/Kuwait'.");

        await _settingManager.SetForCurrentTenantAsync(OrderXChangeSettings.AvailabilityDayEndTime, time);
        await _settingManager.SetForCurrentTenantAsync(OrderXChangeSettings.AvailabilityTimeZone, tzId);

        return await GetSettingsAsync();
    }

    private async Task<(string Time, string TimeZone)> ReadScheduleSettingsAsync()
    {
        var time = await _settingProvider.GetOrNullAsync(OrderXChangeSettings.AvailabilityDayEndTime);
        var tzId = await _settingProvider.GetOrNullAsync(OrderXChangeSettings.AvailabilityTimeZone);
        return (
            string.IsNullOrWhiteSpace(time) ? DefaultDayEndTime : time.Trim(),
            string.IsNullOrWhiteSpace(tzId) ? DefaultTimeZone : tzId.Trim());
    }

    private async Task<DateTime> ResolveForADayRestoreUtcAsync()
    {
        var (time, tzId) = await ReadScheduleSettingsAsync();
        return ComputeNextRestoreUtc(time, tzId);
    }

    /// <summary>Next UTC instant the configured day-end time occurs strictly after "now".</summary>
    private DateTime ComputeNextRestoreUtc(string time, string tzId)
    {
        if (!TryParseTimeOfDay(time, out var hour, out var minute))
        {
            hour = 5;
            minute = 0;
        }

        var tz = TryResolveTimeZone(tzId, out var resolved) ? resolved : TimeZoneInfo.Utc;

        var nowUtc = _clock.Now.Kind switch
        {
            DateTimeKind.Utc => _clock.Now,
            DateTimeKind.Local => _clock.Now.ToUniversalTime(),
            _ => DateTime.SpecifyKind(_clock.Now, DateTimeKind.Utc)
        };

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var candidate = nowLocal.Date.AddHours(hour).AddMinutes(minute);
        if (candidate <= nowLocal)
            candidate = candidate.AddDays(1);

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified), tz);
    }

    private static bool TryParseTimeOfDay(string? value, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (string.IsNullOrWhiteSpace(value) || !value.Contains(':'))
            return false;

        if (!TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var ts)
            || ts < TimeSpan.Zero || ts >= TimeSpan.FromDays(1))
            return false;

        hour = ts.Hours;
        minute = ts.Minutes;
        return true;
    }

    private static bool TryResolveTimeZone(string tzId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return true;
        }
        catch (Exception)
        {
            // Cross-platform alias: Asia/Kuwait maps to "Arab Standard Time" on Windows.
            if (string.Equals(tzId, "Asia/Kuwait", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
                    return true;
                }
                catch (Exception)
                {
                    // fall through to UTC
                }
            }

            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
