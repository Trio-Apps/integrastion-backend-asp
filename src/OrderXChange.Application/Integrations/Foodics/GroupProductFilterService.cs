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
        string? targetGroupLabel = null)
    {
        var result = new GroupFilteredProductsResult();
        var groupIdSet = targetGroupIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "[Group Filter] Starting product filtering. TotalProducts={TotalProducts}, TargetGroup={TargetGroup}, CorrelationId={CorrelationId}",
            allProducts.Count,
            targetGroupLabel ?? "<none>",
            correlationId);

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

        result.FilteredProducts = filteredProducts;
        result.TotalProducts = allProducts.Count;
        result.FilteredCount = filteredProducts.Count;
        result.ProductsWithoutGroups = productsWithoutGroups;
        result.ProductsNotInTargetGroup = productsNotInTargetGroup;
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
