using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Authorization;
using OrderXChange.Domain.Staging;
using OrderXChange.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement.Talabat;

namespace OrderXChange.Search;

/// <summary>
/// Unified "Smart Search" (BRS §4). A single keyword is fanned out across three data sources —
/// orders (<see cref="TalabatOrderSyncLog"/>), catalog items &amp; modifiers
/// (<see cref="FoodicsProductStaging"/>), and branches (derived from <see cref="TalabatAccount"/>).
/// Results are grouped, branch-scoped via <see cref="ICurrentUserBranchProvider"/> (§1), and
/// permission-trimmed: the Orders group requires <c>Orders</c>, the Items group requires
/// <c>Availability</c>, and branches surface when the user can see either feature.
/// </summary>
[Authorize]
public class SmartSearchAppService : ApplicationService, ISmartSearchAppService
{
    private const int DefaultMaxPerGroup = 10;
    private const int MaxPerGroupLimit = 50;

    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderLogRepository;
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
    private readonly IRepository<TalabatAccount, Guid> _talabatAccountRepository;
    private readonly ICurrentUserBranchProvider _branchProvider;
    private readonly IPermissionChecker _permissionChecker;

    public SmartSearchAppService(
        IRepository<TalabatOrderSyncLog, Guid> orderLogRepository,
        IRepository<FoodicsProductStaging, Guid> stagingRepository,
        IRepository<TalabatAccount, Guid> talabatAccountRepository,
        ICurrentUserBranchProvider branchProvider,
        IPermissionChecker permissionChecker)
    {
        _orderLogRepository = orderLogRepository;
        _stagingRepository = stagingRepository;
        _talabatAccountRepository = talabatAccountRepository;
        _branchProvider = branchProvider;
        _permissionChecker = permissionChecker;
    }

    public async Task<SmartSearchResultDto> SearchAsync(SmartSearchInput input)
    {
        var keyword = input.Keyword?.Trim() ?? string.Empty;
        var result = new SmartSearchResultDto { Keyword = keyword };

        // Nothing to search for.
        if (keyword.Length == 0)
            return result;

        var maxPerGroup = input.MaxResultsPerGroup <= 0
            ? DefaultMaxPerGroup
            : Math.Min(input.MaxResultsPerGroup, MaxPerGroupLimit);

        var scope = await _branchProvider.GetScopeAsync();

        // Fail-closed: a restricted user with no branch grants sees nothing anywhere.
        if (!scope.AllBranches && scope.BranchIds.Count == 0)
            return result;

        var canSearchOrders = await _permissionChecker.IsGrantedAsync(OrderXChangePermissions.Orders.Default);
        var canSearchItems = await _permissionChecker.IsGrantedAsync(OrderXChangePermissions.Availability.Default);

        if (canSearchOrders)
            result.Orders = await SearchOrdersAsync(keyword, scope, maxPerGroup);

        if (canSearchItems)
            result.Items = await SearchItemsAsync(keyword, scope, maxPerGroup);

        // Branches are reference data for the Orders / Availability features; surface them
        // (still scoped to the user's accessible branches) when the user can see either.
        if (canSearchOrders || canSearchItems)
            result.Branches = await SearchBranchesAsync(keyword, scope, maxPerGroup);

        result.TotalCount = result.Orders.Count + result.Items.Count + result.Branches.Count;
        return result;
    }

    private async Task<List<SmartSearchOrderDto>> SearchOrdersAsync(string keyword, UserBranchScope scope, int maxPerGroup)
    {
        var queryable = await _orderLogRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        // Branch-scope filter: resolve allowed VendorCodes via TalabatAccount.FoodicsBranchId.
        if (!scope.AllBranches)
        {
            var allowedVendorCodes = await GetAllowedVendorCodesAsync(scope);
            if (allowedVendorCodes.Count == 0)
                return [];

            queryable = queryable.Where(x => allowedVendorCodes.Contains(x.VendorCode));
        }

        var pattern = $"%{keyword}%";
        queryable = queryable.Where(x =>
            EF.Functions.Like(x.VendorCode, pattern)
            || (x.OrderCode != null && EF.Functions.Like(x.OrderCode, pattern))
            || (x.ShortCode != null && EF.Functions.Like(x.ShortCode, pattern))
            || (x.OrderToken != null && EF.Functions.Like(x.OrderToken, pattern))
            || (x.FoodicsOrderId != null && EF.Functions.Like(x.FoodicsOrderId, pattern))
            || (x.CustomerId != null && EF.Functions.Like(x.CustomerId, pattern))
            || (x.CustomerName != null && EF.Functions.Like(x.CustomerName, pattern))
            || (x.CustomerPhone != null && EF.Functions.Like(x.CustomerPhone, pattern))
            || (x.CustomerAddress != null && EF.Functions.Like(x.CustomerAddress, pattern))
            || EF.Functions.Like(x.Status, pattern));

        return await queryable
            .OrderByDescending(x => x.ReceivedAt)
            .Take(maxPerGroup)
            .Select(x => new SmartSearchOrderDto
            {
                Id = x.Id,
                OrderCode = x.OrderCode,
                ShortCode = x.ShortCode,
                OrderToken = x.OrderToken,
                VendorCode = x.VendorCode,
                CustomerName = x.CustomerName,
                CustomerPhone = x.CustomerPhone,
                Status = x.Status,
                ReceivedAt = x.ReceivedAt
            })
            .ToListAsync();
    }

    private async Task<List<SmartSearchItemDto>> SearchItemsAsync(string keyword, UserBranchScope scope, int maxPerGroup)
    {
        var queryable = await _stagingRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking().Where(p => !p.IsDeleted);

        // Branch-scope filter: restrict to the user's Foodics accounts and, for branch-specific
        // rows, to the user's branches (account-level rows with a null BranchId still qualify).
        if (!scope.AllBranches)
        {
            var (allowedAccountIds, allowedBranchIds) = await GetAllowedAccountAndBranchIdsAsync(scope);
            if (allowedAccountIds.Count == 0)
                return [];

            queryable = queryable.Where(p =>
                allowedAccountIds.Contains(p.FoodicsAccountId)
                && (p.BranchId == null || allowedBranchIds.Contains(p.BranchId)));
        }

        var pattern = $"%{keyword}%";
        queryable = queryable.Where(p =>
            EF.Functions.Like(p.Name, pattern)
            || (p.NameLocalized != null && EF.Functions.Like(p.NameLocalized, pattern))
            || (p.Sku != null && EF.Functions.Like(p.Sku, pattern))
            || (p.Barcode != null && EF.Functions.Like(p.Barcode, pattern))
            || EF.Functions.Like(p.FoodicsProductId, pattern)
            || (p.CategoryName != null && EF.Functions.Like(p.CategoryName, pattern))
            // Modifier match: ModifiersJson holds the product's modifier options; a LIKE here
            // surfaces items whose modifier names/options contain the keyword.
            || (p.ModifiersJson != null && EF.Functions.Like(p.ModifiersJson, pattern)));

        var rows = await queryable
            .OrderBy(p => p.Name)
            .Take(maxPerGroup)
            .Select(p => new ItemProjection
            {
                Id = p.Id,
                FoodicsProductId = p.FoodicsProductId,
                Name = p.Name,
                NameLocalized = p.NameLocalized,
                Sku = p.Sku,
                CategoryName = p.CategoryName,
                IsActive = p.IsActive,
                ModifiersJson = p.ModifiersJson
            })
            .ToListAsync();

        return rows
            .Select(p => new SmartSearchItemDto
            {
                Id = p.Id,
                FoodicsProductId = p.FoodicsProductId,
                Name = p.Name,
                NameLocalized = p.NameLocalized,
                Sku = p.Sku,
                CategoryName = p.CategoryName,
                IsActive = p.IsActive,
                MatchedModifiers = ExtractMatchingModifierNames(p.ModifiersJson, keyword)
            })
            .ToList();
    }

    private async Task<List<SmartSearchBranchDto>> SearchBranchesAsync(string keyword, UserBranchScope scope, int maxPerGroup)
    {
        var accountQueryable = await _talabatAccountRepository.GetQueryableAsync();
        accountQueryable = accountQueryable.AsNoTracking().Where(a => a.FoodicsBranchId != null);

        if (!scope.AllBranches)
        {
            var branchIds = scope.BranchIds.ToList();
            accountQueryable = accountQueryable.Where(a => branchIds.Contains(a.FoodicsBranchId!));
        }

        var pattern = $"%{keyword}%";
        accountQueryable = accountQueryable.Where(a =>
            EF.Functions.Like(a.FoodicsBranchId!, pattern)
            || (a.FoodicsBranchName != null && EF.Functions.Like(a.FoodicsBranchName, pattern))
            || EF.Functions.Like(a.VendorCode, pattern));

        var rows = await accountQueryable
            .Select(a => new { a.FoodicsBranchId, a.FoodicsBranchName })
            .ToListAsync();

        // A branch can be served by more than one vendor account — group by branch id.
        return rows
            .GroupBy(a => a.FoodicsBranchId!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SmartSearchBranchDto
            {
                BranchId = g.Key,
                BranchName = g.Select(x => x.FoodicsBranchName)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key
            })
            .OrderBy(b => b.BranchName, StringComparer.OrdinalIgnoreCase)
            .Take(maxPerGroup)
            .ToList();
    }

    private async Task<List<string>> GetAllowedVendorCodesAsync(UserBranchScope scope)
    {
        var branchIds = scope.BranchIds.ToList();
        var accountQueryable = await _talabatAccountRepository.GetQueryableAsync();
        return await accountQueryable
            .Where(a => a.FoodicsBranchId != null && branchIds.Contains(a.FoodicsBranchId))
            .Select(a => a.VendorCode)
            .Distinct()
            .ToListAsync();
    }

    private async Task<(List<Guid> AccountIds, List<string> BranchIds)> GetAllowedAccountAndBranchIdsAsync(UserBranchScope scope)
    {
        var branchIds = scope.BranchIds.ToList();
        var accountQueryable = await _talabatAccountRepository.GetQueryableAsync();
        var accountIds = await accountQueryable
            .Where(a => a.FoodicsBranchId != null
                        && branchIds.Contains(a.FoodicsBranchId)
                        && a.FoodicsAccountId != null)
            .Select(a => a.FoodicsAccountId!.Value)
            .Distinct()
            .ToListAsync();

        return (accountIds, branchIds);
    }

    /// <summary>
    /// Best-effort parse of a product's <c>ModifiersJson</c> to surface the modifier / option
    /// names that contain the keyword. Never throws — malformed JSON yields an empty list.
    /// </summary>
    private static List<string> ExtractMatchingModifierNames(string? modifiersJson, string keyword)
    {
        if (string.IsNullOrWhiteSpace(modifiersJson))
            return [];

        var matches = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(modifiersJson);
            CollectModifierNames(doc.RootElement, keyword, matches);
        }
        catch (JsonException)
        {
            return [];
        }

        return matches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static void CollectModifierNames(JsonElement element, string keyword, List<string> matches)
    {
        if (matches.Count >= 20)
            return; // safety cap against pathological payloads

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var isNameProperty = prop.Name.Contains("name", StringComparison.OrdinalIgnoreCase);
                    if (isNameProperty && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(value)
                            && value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add(value);
                        }
                    }
                    else
                    {
                        CollectModifierNames(prop.Value, keyword, matches);
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectModifierNames(item, keyword, matches);
                }
                break;
        }
    }

    private sealed class ItemProjection
    {
        public Guid Id { get; set; }
        public string? FoodicsProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameLocalized { get; set; }
        public string? Sku { get; set; }
        public string? CategoryName { get; set; }
        public bool IsActive { get; set; }
        public string? ModifiersJson { get; set; }
    }
}
