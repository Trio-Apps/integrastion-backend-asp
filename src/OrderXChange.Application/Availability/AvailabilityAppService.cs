using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Authorization;
using OrderXChange.Domain.Staging;
using OrderXChange.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
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

    public AvailabilityAppService(
        IRepository<FoodicsProductStaging, Guid> stagingRepo,
        IRepository<ItemAvailabilityState, Guid> availabilityRepo,
        IRepository<TalabatAccount, Guid> talabatAccountRepo,
        ICurrentUserBranchProvider branchProvider,
        IClock clock,
        IDataFilter dataFilter)
    {
        _stagingRepo = stagingRepo;
        _availabilityRepo = availabilityRepo;
        _talabatAccountRepo = talabatAccountRepo;
        _branchProvider = branchProvider;
        _clock = clock;
        _dataFilter = dataFilter;
    }

    public async Task<PagedResultDto<AvailabilityItemDto>> GetItemsAsync(GetAvailabilityInput input)
    {
        // Vendors (branches) come from TalabatAccount, not from staging.
        var vendors = await ResolveVendorsAsync();
        if (vendors.Count == 0)
            return new PagedResultDto<AvailabilityItemDto>(0, new List<AvailabilityItemDto>());

        var vendor = !string.IsNullOrWhiteSpace(input.VendorCode)
            ? vendors.FirstOrDefault(v => v.Code == input.VendorCode!.Trim())
            : vendors[0];
        if (vendor.Code == null)
            return new PagedResultDto<AvailabilityItemDto>(0, new List<AvailabilityItemDto>());

        var query = (await _stagingRepo.GetQueryableAsync()).AsNoTracking()
            .Where(x => !x.IsDeleted && x.FoodicsAccountId == vendor.AccountId);

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
            .Select(x => new { x.FoodicsProductId, x.Name, x.NameLocalized, x.Sku, x.CategoryName })
            .ToListAsync();

        var productIds = rows.Select(r => r.FoodicsProductId).ToList();
        var stateQuery = await _availabilityRepo.GetQueryableAsync();
        var states = await stateQuery
            .Where(a => a.VendorCode == vendor.Code && productIds.Contains(a.FoodicsProductId))
            .Select(a => new { a.FoodicsProductId, a.IsInStock, a.Mode, a.RestoreAtUtc })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var st = states.FirstOrDefault(s => s.FoodicsProductId == r.FoodicsProductId);
            return new AvailabilityItemDto
            {
                FoodicsProductId = r.FoodicsProductId,
                Name = r.Name,
                NameLocalized = r.NameLocalized,
                Sku = r.Sku,
                CategoryName = r.CategoryName,
                VendorCode = vendor.Code,
                IsInStock = st?.IsInStock ?? true,
                Mode = st?.Mode,
                RestoreAtUtc = st?.RestoreAtUtc,
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

        var now = _clock.Now;
        var mode = input.Mode == AvailabilityMode.ForADay ? AvailabilityMode.ForADay : AvailabilityMode.TillFurtherNotice;
        DateTime? restoreAt = mode == AvailabilityMode.ForADay ? now.Date.AddDays(1).AddHours(8) : null;

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

        // NOTE: pushing the state to Talabat's availability API is a follow-up (§5).
    }

    private async Task<List<(string Code, Guid AccountId)>> ResolveVendorsAsync()
    {
        var scope = await _branchProvider.GetScopeAsync();
        var query = (await _talabatAccountRepo.GetQueryableAsync()).Where(a => a.IsActive);
        if (!scope.AllBranches)
        {
            if (scope.BranchIds.Count == 0)
                return new List<(string, Guid)>();
            query = query.Where(a => a.FoodicsBranchId != null && scope.BranchIds.Contains(a.FoodicsBranchId));
        }

        var list = await query
            .Where(a => a.FoodicsAccountId != null)
            .Select(a => new { a.VendorCode, a.FoodicsAccountId })
            .Distinct()
            .ToListAsync();

        return list.Select(x => (x.VendorCode, x.FoodicsAccountId!.Value)).ToList();
    }
}
