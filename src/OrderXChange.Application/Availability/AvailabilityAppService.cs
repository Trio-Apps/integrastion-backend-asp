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

    public AvailabilityAppService(
        IRepository<FoodicsProductStaging, Guid> stagingRepo,
        IRepository<ItemAvailabilityState, Guid> availabilityRepo,
        IRepository<TalabatAccount, Guid> talabatAccountRepo,
        ICurrentUserBranchProvider branchProvider,
        IClock clock)
    {
        _stagingRepo = stagingRepo;
        _availabilityRepo = availabilityRepo;
        _talabatAccountRepo = talabatAccountRepo;
        _branchProvider = branchProvider;
        _clock = clock;
    }

    public async Task<PagedResultDto<AvailabilityItemDto>> GetItemsAsync(GetAvailabilityInput input)
    {
        var allowedVendors = await ResolveAllowedVendorsAsync();
        if (allowedVendors is { Count: 0 })
            return new PagedResultDto<AvailabilityItemDto>(0, new List<AvailabilityItemDto>());

        var query = (await _stagingRepo.GetQueryableAsync()).AsNoTracking()
            .Where(x => !x.IsDeleted && x.TalabatVendorCode != null);

        if (allowedVendors != null)
            query = query.Where(x => allowedVendors.Contains(x.TalabatVendorCode!));

        if (!string.IsNullOrWhiteSpace(input.VendorCode))
        {
            var vendor = input.VendorCode.Trim();
            query = query.Where(x => x.TalabatVendorCode == vendor);
        }

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
            .Select(x => new { x.FoodicsProductId, x.Name, x.NameLocalized, x.Sku, x.CategoryName, Vendor = x.TalabatVendorCode! })
            .ToListAsync();

        var productIds = rows.Select(r => r.FoodicsProductId).Distinct().ToList();
        var stateQuery = await _availabilityRepo.GetQueryableAsync();
        var states = await stateQuery
            .Where(a => productIds.Contains(a.FoodicsProductId))
            .Select(a => new { a.FoodicsProductId, a.VendorCode, a.IsInStock, a.Mode, a.RestoreAtUtc })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var st = states.FirstOrDefault(s => s.FoodicsProductId == r.FoodicsProductId && s.VendorCode == r.Vendor);
            return new AvailabilityItemDto
            {
                FoodicsProductId = r.FoodicsProductId,
                Name = r.Name,
                NameLocalized = r.NameLocalized,
                Sku = r.Sku,
                CategoryName = r.CategoryName,
                VendorCode = r.Vendor,
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

        var allowedVendors = await ResolveAllowedVendorsAsync();
        var vendorCodes = input.VendorCodes.Distinct().ToList();
        if (allowedVendors != null)
            vendorCodes = vendorCodes.Where(v => allowedVendors.Contains(v)).ToList();
        if (vendorCodes.Count == 0)
            return; // fail-closed: nothing in the caller's branch scope

        // Resolve the (product, vendor, account) triples from staging so we only touch real items.
        var productIds = input.FoodicsProductIds.Distinct().ToList();
        var stagingQuery = await _stagingRepo.GetQueryableAsync();
        var targets = await stagingQuery
            .Where(x => productIds.Contains(x.FoodicsProductId) && x.TalabatVendorCode != null && vendorCodes.Contains(x.TalabatVendorCode))
            .Select(x => new { x.FoodicsProductId, Vendor = x.TalabatVendorCode!, x.FoodicsAccountId })
            .Distinct()
            .ToListAsync();

        var now = _clock.Now;
        var mode = input.Mode == AvailabilityMode.ForADay ? AvailabilityMode.ForADay : AvailabilityMode.TillFurtherNotice;
        DateTime? restoreAt = mode == AvailabilityMode.ForADay ? now.Date.AddDays(1).AddHours(8) : null;

        foreach (var t in targets)
        {
            var existing = await _availabilityRepo.FirstOrDefaultAsync(
                a => a.FoodicsProductId == t.FoodicsProductId && a.VendorCode == t.Vendor);

            if (input.InStock)
            {
                // Back to the default (in stock) — remove any out-of-stock override.
                if (existing != null)
                    await _availabilityRepo.DeleteAsync(existing);
            }
            else if (existing == null)
            {
                await _availabilityRepo.InsertAsync(new ItemAvailabilityState
                {
                    FoodicsAccountId = t.FoodicsAccountId,
                    FoodicsProductId = t.FoodicsProductId,
                    VendorCode = t.Vendor,
                    IsInStock = false,
                    Mode = mode,
                    RestoreAtUtc = restoreAt,
                });
            }
            else
            {
                existing.IsInStock = false;
                existing.Mode = mode;
                existing.RestoreAtUtc = restoreAt;
                await _availabilityRepo.UpdateAsync(existing);
            }
        }

        // NOTE: pushing the out-of-stock state to Talabat's availability API is wired in a
        // follow-up (§5: migrate TalabatCatalogClient to the current /catalog/items/availability
        // contract). The DB state above is the source of truth the console + auto-restore use.
    }

    /// <summary>Null = all vendors (admin / Branches.All). Otherwise the vendor codes the caller may manage.</summary>
    private async Task<List<string>?> ResolveAllowedVendorsAsync()
    {
        var scope = await _branchProvider.GetScopeAsync();
        if (scope.AllBranches)
            return null;
        if (scope.BranchIds.Count == 0)
            return new List<string>();

        var accountQuery = await _talabatAccountRepo.GetQueryableAsync();
        return await accountQuery
            .Where(a => a.FoodicsBranchId != null && scope.BranchIds.Contains(a.FoodicsBranchId))
            .Select(a => a.VendorCode)
            .Distinct()
            .ToListAsync();
    }
}
