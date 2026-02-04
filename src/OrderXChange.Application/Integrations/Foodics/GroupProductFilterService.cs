using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

/// <summary>
/// Service responsible for filtering Foodics products based on group membership.
/// This ensures that only products belonging to a specific group are synced to Talabat.
/// Products must belong to the configured group to be included in sync operations.
/// </summary>
public class GroupProductFilterService : ITransientDependency
{
    private readonly ILogger<GroupProductFilterService> _logger;

    public GroupProductFilterService(ILogger<GroupProductFilterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Filters products based on group membership.
    /// </summary>
    /// <param name="allProducts">All products from Foodics</param>
    /// <param name="targetGroupId">Target group ID to filter by (null = no filtering)</param>
    /// <param name="correlationId">Correlation ID for logging</param>
    /// <returns>Filtered products belonging to the target group</returns>
    public GroupFilteredProductsResult FilterProductsByGroup(
        Dictionary<string, FoodicsProductDetailDto> allProducts,
        string? targetGroupId,
        string correlationId)
    {
        var result = new GroupFilteredProductsResult();

        _logger.LogInformation(
            "🏷️ [Group Filter] Starting product filtering. " +
            "TotalProducts={TotalProducts}, TargetGroup={TargetGroup}, " +
            "CorrelationId={CorrelationId}",
            allProducts.Count, targetGroupId ?? "<none>", correlationId);

        // If no specific group ID, return all products (no group filtering)
        if (string.IsNullOrWhiteSpace(targetGroupId))
        {
            result.FilteredProducts = allProducts.Values.ToList();
            result.TotalProducts = allProducts.Count;
            result.FilteredCount = allProducts.Count;
            result.FilterReason = "No target group specified - returning all products";

            _logger.LogInformation(
                "🏷️ [Group Filter] No target group specified - returning all products. " +
                "Products={Count}, CorrelationId={CorrelationId}",
                result.FilteredCount, correlationId);

            return result;
        }

        // Filter products by group membership
        var filteredProducts = new List<FoodicsProductDetailDto>();
        var productsWithoutGroups = 0;
        var productsNotInTargetGroup = 0;
        var productsInTargetGroup = 0;

        foreach (var product in allProducts.Values)
        {
            // Check if product has group information
            if (product.Groups == null || !product.Groups.Any())
            {
                productsWithoutGroups++;
                // Products with NO group assignments should be EXCLUDED when filtering by specific group
                _logger.LogDebug(
                    "🏷️ [Group Filter] Product has no group assignments - EXCLUDED. " +
                    "ProductId={ProductId}, ProductName={ProductName}, " +
                    "TargetGroup={TargetGroup}, CorrelationId={CorrelationId}",
                    product.Id, product.Name, targetGroupId, correlationId);
                continue;
            }

            // Check if product belongs to the target group
            var belongsToGroup = product.Groups.Any(g =>
                string.Equals(g.Id, targetGroupId, StringComparison.OrdinalIgnoreCase));

            if (belongsToGroup)
            {
                filteredProducts.Add(product);
                productsInTargetGroup++;
            }
            else
            {
                productsNotInTargetGroup++;

                _logger.LogDebug(
                    "🏷️ [Group Filter] Product not in target group. " +
                    "ProductId={ProductId}, ProductName={ProductName}, " +
                    "TargetGroup={TargetGroup}, AvailableGroups={AvailableGroups}, " +
                    "CorrelationId={CorrelationId}",
                    product.Id, product.Name, targetGroupId,
                    string.Join(", ", product.Groups.Select(g => g.Id ?? "null")),
                    correlationId);
            }
        }

        result.FilteredProducts = filteredProducts;
        result.TotalProducts = allProducts.Count;
        result.FilteredCount = filteredProducts.Count;
        result.ProductsWithoutGroups = productsWithoutGroups;
        result.ProductsNotInTargetGroup = productsNotInTargetGroup;
        result.FilterReason = $"Filtered by group: {targetGroupId}";

        _logger.LogInformation(
            "🏷️ [Group Filter] Filtering completed. " +
            "TotalProducts={TotalProducts}, ProductsInTargetGroup={ProductsInTargetGroup}, " +
            "ProductsWithoutGroups={ProductsWithoutGroups} (EXCLUDED), " +
            "ProductsNotInTargetGroup={ProductsNotInTargetGroup} (EXCLUDED), " +
            "TargetGroup={TargetGroup}, CorrelationId={CorrelationId}",
            result.TotalProducts, result.FilteredCount, result.ProductsWithoutGroups,
            result.ProductsNotInTargetGroup, targetGroupId, correlationId);

        return result;
    }

    /// <summary>
    /// Validates that a group ID exists and has products.
    /// </summary>
    /// <param name="allProducts">All products from Foodics</param>
    /// <param name="groupId">Group ID to validate</param>
    /// <returns>Validation result</returns>
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
                       p.Groups.Any(g => string.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var groupExists = productsInGroup.Any();

        // Get group name from first matching product's group
        string? groupName = null;
        if (groupExists)
        {
            var matchingGroup = productsInGroup.First().Groups?
                .FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase));
            groupName = matchingGroup?.Name;
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

    /// <summary>
    /// Gets all unique groups from products.
    /// </summary>
    /// <param name="allProducts">All products from Foodics</param>
    /// <returns>List of unique groups with product counts</returns>
    public List<GroupSummary> GetAllGroups(Dictionary<string, FoodicsProductDetailDto> allProducts)
    {
        var groupSummaries = allProducts.Values
            .Where(p => p.Groups != null && p.Groups.Any())
            .SelectMany(p => p.Groups!)
            .GroupBy(g => g.Id)
            .Select(grp =>
            {
                var firstGroup = grp.First();
                var productCount = allProducts.Values.Count(p =>
                    p.Groups != null &&
                    p.Groups.Any(pg => string.Equals(pg.Id, firstGroup.Id, StringComparison.OrdinalIgnoreCase)));

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

        return groupSummaries;
    }
}

/// <summary>
/// Result of product filtering by group
/// </summary>
public class GroupFilteredProductsResult
{
    public List<FoodicsProductDetailDto> FilteredProducts { get; set; } = new();
    public int TotalProducts { get; set; }
    public int FilteredCount { get; set; }
    public int ProductsWithoutGroups { get; set; }
    public int ProductsNotInTargetGroup { get; set; }
    public string FilterReason { get; set; } = string.Empty;
}

/// <summary>
/// Result of group validation
/// </summary>
public class GroupValidationResult
{
    public bool IsValid { get; set; }
    public int ProductCount { get; set; }
    public string? GroupName { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Summary of a group with product count
/// </summary>
public class GroupSummary
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public int ProductCount { get; set; }
}

