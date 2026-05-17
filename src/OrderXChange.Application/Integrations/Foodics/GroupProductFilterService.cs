using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

/// <summary>
/// Filters Foodics products by group membership for Talabat publishing.
/// Products must belong to the configured group scope and be active within that scope.
/// </summary>
public class GroupProductFilterService : ITransientDependency
{
    private readonly ILogger<GroupProductFilterService> _logger;

    public GroupProductFilterService(ILogger<GroupProductFilterService> logger)
    {
        _logger = logger;
    }

    public GroupFilteredProductsResult FilterProductsByGroup(
        Dictionary<string, FoodicsProductDetailDto> allProducts,
        string? targetGroupId,
        string correlationId)
    {
        var targetGroupIds = string.IsNullOrWhiteSpace(targetGroupId)
            ? null
            : new[] { targetGroupId };

        return FilterProductsByGroups(allProducts, targetGroupIds, correlationId, targetGroupId);
    }

    public GroupFilteredProductsResult FilterProductsByGroups(
        Dictionary<string, FoodicsProductDetailDto> allProducts,
        IReadOnlyCollection<string>? targetGroupIds,
        string correlationId,
        string? targetGroupLabel = null,
        IReadOnlyCollection<string>? targetProductIds = null)
    {
        var result = new GroupFilteredProductsResult();
        var groupIdSet = targetGroupIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var productIdSet = targetProductIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "[Group Filter] Starting product filtering. TotalProducts={TotalProducts}, TargetGroup={TargetGroup}, TargetProducts={TargetProducts}, CorrelationId={CorrelationId}",
            allProducts.Count,
            targetGroupLabel ?? "<none>",
            productIdSet?.Count ?? 0,
            correlationId);

        if (productIdSet is { Count: > 0 })
        {
            var filteredByProductIds = allProducts.Values
                .Where(product => !string.IsNullOrWhiteSpace(product.Id) && productIdSet.Contains(product.Id))
                .ToList();

            var fallbackByGroupMembership = groupIdSet is { Count: > 0 }
                ? FilterByGroupMembership(allProducts, groupIdSet)
                : new GroupMembershipFilterResult();

            if (fallbackByGroupMembership.FilteredProducts.Count > filteredByProductIds.Count)
            {
                result.FilteredProducts = fallbackByGroupMembership.FilteredProducts;
                result.TotalProducts = allProducts.Count;
                result.FilteredCount = fallbackByGroupMembership.FilteredProducts.Count;
                result.ProductsWithoutGroups = fallbackByGroupMembership.ProductsWithoutGroups;
                result.ProductsNotInTargetGroup = fallbackByGroupMembership.ProductsNotInTargetGroup;
                result.FilterReason = $"Filtered by group membership fallback: {targetGroupLabel}";

                _logger.LogWarning(
                    "[Group Filter] Explicit group product list did not match the fetched Foodics products; using group membership fallback. TotalProducts={TotalProducts}, ExplicitMatches={ExplicitMatches}, MembershipMatches={MembershipMatches}, TargetProducts={TargetProducts}, TargetGroup={TargetGroup}, CorrelationId={CorrelationId}",
                    result.TotalProducts,
                    filteredByProductIds.Count,
                    fallbackByGroupMembership.FilteredProducts.Count,
                    productIdSet.Count,
                    targetGroupLabel ?? "<none>",
                    correlationId);

                return result;
            }

            result.FilteredProducts = filteredByProductIds;
            result.TotalProducts = allProducts.Count;
            result.FilteredCount = filteredByProductIds.Count;
            result.ProductsWithoutGroups = allProducts.Count(p => p.Value.Groups == null || p.Value.Groups.Count == 0);
            result.ProductsNotInTargetGroup = allProducts.Count - filteredByProductIds.Count;
            result.FilterReason = $"Filtered by group product list: {targetGroupLabel}";

            _logger.LogInformation(
                "[Group Filter] Filtering completed by explicit group product list. TotalProducts={TotalProducts}, ProductsInTargetGroup={ProductsInTargetGroup}, TargetProducts={TargetProducts}, TargetGroup={TargetGroup}, CorrelationId={CorrelationId}",
                result.TotalProducts,
                result.FilteredCount,
                productIdSet.Count,
                targetGroupLabel ?? "<none>",
                correlationId);

            return result;
        }

        if (groupIdSet == null)
        {
            result.FilteredProducts = allProducts.Values.ToList();
            result.TotalProducts = allProducts.Count;
            result.FilteredCount = allProducts.Count;
            result.FilterReason = "No target group specified - returning all products";

            _logger.LogInformation(
                "[Group Filter] No target group specified - returning all products. Products={Count}, CorrelationId={CorrelationId}",
                result.FilteredCount,
                correlationId);

            return result;
        }

        if (groupIdSet.Count == 0)
        {
            result.FilteredProducts = new List<FoodicsProductDetailDto>();
            result.TotalProducts = allProducts.Count;
            result.FilteredCount = 0;
            result.ProductsWithoutGroups = allProducts.Count(p => p.Value.Groups == null || p.Value.Groups.Count == 0);
            result.ProductsNotInTargetGroup = allProducts.Count - result.ProductsWithoutGroups;
            result.FilterReason = $"Target group scope '{targetGroupLabel ?? "<empty>"}' resolved to no active groups";

            _logger.LogWarning(
                "[Group Filter] Target group scope resolved to no active groups. TotalProducts={TotalProducts}, TargetGroup={TargetGroup}, CorrelationId={CorrelationId}",
                result.TotalProducts,
                targetGroupLabel ?? "<empty>",
                correlationId);

            return result;
        }

        var membershipResult = FilterByGroupMembership(allProducts, groupIdSet);

        result.FilteredProducts = membershipResult.FilteredProducts;
        result.TotalProducts = allProducts.Count;
        result.FilteredCount = membershipResult.FilteredProducts.Count;
        result.ProductsWithoutGroups = membershipResult.ProductsWithoutGroups;
        result.ProductsNotInTargetGroup = membershipResult.ProductsNotInTargetGroup;
        result.FilterReason = $"Filtered by group: {targetGroupLabel}";

        _logger.LogInformation(
            "[Group Filter] Filtering completed. TotalProducts={TotalProducts}, ProductsInTargetGroup={ProductsInTargetGroup}, ProductsWithoutGroups={ProductsWithoutGroups} (EXCLUDED), ProductsNotInTargetGroup={ProductsNotInTargetGroup} (EXCLUDED), TargetGroup={TargetGroup}, CorrelationId={CorrelationId}",
            result.TotalProducts,
            result.FilteredCount,
            result.ProductsWithoutGroups,
            result.ProductsNotInTargetGroup,
            targetGroupLabel ?? "<none>",
            correlationId);

        return result;
    }

    private static GroupMembershipFilterResult FilterByGroupMembership(
        Dictionary<string, FoodicsProductDetailDto> allProducts,
        HashSet<string> groupIdSet)
    {
        var filteredProducts = new List<FoodicsProductDetailDto>();
        var productsWithoutGroups = 0;
        var productsNotInTargetGroup = 0;

        foreach (var product in allProducts.Values)
        {
            if (product.Groups == null || product.Groups.Count == 0)
            {
                productsWithoutGroups++;
                continue;
            }

            var belongsToScope = product.Groups.Any(g =>
                !string.IsNullOrWhiteSpace(g.Id) &&
                groupIdSet.Contains(g.Id) &&
                g.Pivot?.IsActive != false);

            if (belongsToScope)
            {
                filteredProducts.Add(product);
            }
            else
            {
                productsNotInTargetGroup++;
            }
        }

        return new GroupMembershipFilterResult(
            filteredProducts,
            productsWithoutGroups,
            productsNotInTargetGroup);
    }

    public GroupValidationResult ValidateGroup(
        Dictionary<string, FoodicsProductDetailDto> allProducts,
        string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return new GroupValidationResult
            {
                IsValid = false,
                Message = "Group ID is null or empty"
            };
        }

        var productsInGroup = allProducts.Values
            .Where(p => p.Groups != null &&
                        p.Groups.Any(g =>
                            string.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase) &&
                            g.Pivot?.IsActive != false))
            .ToList();

        var groupExists = productsInGroup.Count > 0;
        string? groupName = null;
        if (groupExists)
        {
            groupName = productsInGroup.First().Groups?
                .FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }

        return new GroupValidationResult
        {
            IsValid = groupExists,
            ProductCount = productsInGroup.Count,
            GroupName = groupName,
            Message = groupExists
                ? $"Group {groupId} ({groupName}) found with {productsInGroup.Count} products"
                : $"Group {groupId} not found or has no products"
        };
    }

    public List<GroupSummary> GetAllGroups(Dictionary<string, FoodicsProductDetailDto> allProducts)
    {
        return allProducts.Values
            .Where(p => p.Groups != null && p.Groups.Count > 0)
            .SelectMany(p => p.Groups!)
            .Where(g => !string.IsNullOrWhiteSpace(g.Id) && g.Pivot?.IsActive != false)
            .GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .Select(grp =>
            {
                var firstGroup = grp.First();
                var productCount = allProducts.Values.Count(p =>
                    p.Groups != null &&
                    p.Groups.Any(pg =>
                        string.Equals(pg.Id, firstGroup.Id, StringComparison.OrdinalIgnoreCase) &&
                        pg.Pivot?.IsActive != false));

                return new GroupSummary
                {
                    Id = firstGroup.Id,
                    Name = firstGroup.Name,
                    NameLocalized = firstGroup.NameLocalized,
                    ProductCount = productCount
                };
            })
            .OrderBy(g => g.Name)
            .ToList();
    }
}

public class GroupFilteredProductsResult
{
    public List<FoodicsProductDetailDto> FilteredProducts { get; set; } = new();
    public int TotalProducts { get; set; }
    public int FilteredCount { get; set; }
    public int ProductsWithoutGroups { get; set; }
    public int ProductsNotInTargetGroup { get; set; }
    public string FilterReason { get; set; } = string.Empty;
}

internal sealed record GroupMembershipFilterResult(
    List<FoodicsProductDetailDto> FilteredProducts,
    int ProductsWithoutGroups,
    int ProductsNotInTargetGroup)
{
    public GroupMembershipFilterResult()
        : this(new List<FoodicsProductDetailDto>(), 0, 0)
    {
    }
}

public class GroupValidationResult
{
    public bool IsValid { get; set; }
    public int ProductCount { get; set; }
    public string? GroupName { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class GroupSummary
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public int ProductCount { get; set; }
}
