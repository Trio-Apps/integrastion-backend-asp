using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

/// <summary>
/// Service responsible for filtering Foodics products based on branch availability.
/// This ensures that only products available in specific branches are synced to Talabat.
/// </summary>
public class BranchProductFilterService : ITransientDependency
{
    private readonly ILogger<BranchProductFilterService> _logger;

    public BranchProductFilterService(ILogger<BranchProductFilterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Filters products based on branch availability.
    /// </summary>
    /// <param name="allProducts">All products from Foodics</param>
    /// <param name="targetBranchId">Target branch ID to filter by (null = no filtering)</param>
    /// <param name="syncAllBranches">Whether to sync all branches regardless of targetBranchId</param>
    /// <param name="correlationId">Correlation ID for logging</param>
    /// <returns>Filtered products available in the target branch</returns>
    public FilteredProductsResult FilterProductsByBranch(
        Dictionary<string, FoodicsProductDetailDto> allProducts,
        string? targetBranchId,
        bool syncAllBranches,
        string correlationId)
    {
        var result = new FilteredProductsResult();
        
        _logger.LogInformation(
            "🏢 [Branch Filter] Starting product filtering. " +
            "TotalProducts={TotalProducts}, TargetBranch={TargetBranch}, " +
            "SyncAllBranches={SyncAllBranches}, CorrelationId={CorrelationId}",
            allProducts.Count, targetBranchId ?? "<none>", syncAllBranches, correlationId);

        // If syncAllBranches is true, return all products (legacy behavior)
        if (syncAllBranches)
        {
            result.FilteredProducts = allProducts.Values.ToList();
            result.TotalProducts = allProducts.Count;
            result.FilteredCount = allProducts.Count;
            result.FilterReason = "SyncAllBranches=true (legacy mode)";
            
            _logger.LogInformation(
                "🏢 [Branch Filter] No filtering applied - sync all branches mode. " +
                "Products={Count}, CorrelationId={CorrelationId}",
                result.FilteredCount, correlationId);
                
            return result;
        }

        // If no specific branch ID, return all products
        if (string.IsNullOrWhiteSpace(targetBranchId))
        {
            result.FilteredProducts = allProducts.Values.ToList();
            result.TotalProducts = allProducts.Count;
            result.FilteredCount = allProducts.Count;
            result.FilterReason = "No target branch specified";
            
            _logger.LogInformation(
                "🏢 [Branch Filter] No target branch specified - returning all products. " +
                "Products={Count}, CorrelationId={CorrelationId}",
                result.FilteredCount, correlationId);
                
            return result;
        }

        // Filter products by branch availability
        var filteredProducts = new List<FoodicsProductDetailDto>();
        var productsWithoutBranches = 0;
        var productsNotInTargetBranch = 0;
        var productsInTargetBranch = 0;

        foreach (var product in allProducts.Values)
        {
            // Check if product has branch information
            if (product.Branches == null || !product.Branches.Any())
            {
                productsWithoutBranches++;
                // FIXED: Products with NO branch assignments should be EXCLUDED when filtering by specific branch
                // A product with empty branches means it's not assigned to ANY branch
                _logger.LogDebug(
                    "🏢 [Branch Filter] Product has no branch assignments - EXCLUDED. " +
                    "ProductId={ProductId}, ProductName={ProductName}, " +
                    "TargetBranch={TargetBranch}, CorrelationId={CorrelationId}",
                    product.Id, product.Name, targetBranchId, correlationId);
                continue;
            }

            // Check if product is available in target branch AND is_active in that branch
            var branchInfo = product.Branches.FirstOrDefault(branch => 
                string.Equals(branch.Id, targetBranchId, StringComparison.OrdinalIgnoreCase));

            if (branchInfo != null)
            {
                // Product is assigned to target branch
                // Check if it's active in that branch (pivot.is_active)
                var isActiveInBranch = branchInfo.Pivot?.IsActive ?? true;
                
                if (isActiveInBranch)
                {
                    filteredProducts.Add(product);
                    productsInTargetBranch++;
                }
                else
                {
                    productsNotInTargetBranch++;
                    _logger.LogDebug(
                        "🏢 [Branch Filter] Product is INACTIVE in target branch - EXCLUDED. " +
                        "ProductId={ProductId}, ProductName={ProductName}, " +
                        "TargetBranch={TargetBranch}, IsActiveInBranch={IsActive}, " +
                        "CorrelationId={CorrelationId}",
                        product.Id, product.Name, targetBranchId, isActiveInBranch, correlationId);
                }
            }
            else
            {
                productsNotInTargetBranch++;
                
                _logger.LogDebug(
                    "🏢 [Branch Filter] Product not available in target branch. " +
                    "ProductId={ProductId}, ProductName={ProductName}, " +
                    "TargetBranch={TargetBranch}, AvailableBranches={AvailableBranches}, " +
                    "CorrelationId={CorrelationId}",
                    product.Id, product.Name, targetBranchId,
                    string.Join(", ", product.Branches.Select(b => b.Id ?? "null")),
                    correlationId);
            }
        }

        result.FilteredProducts = filteredProducts;
        result.TotalProducts = allProducts.Count;
        result.FilteredCount = filteredProducts.Count;
        result.ProductsWithoutBranches = productsWithoutBranches;
        result.ProductsNotInTargetBranch = productsNotInTargetBranch;
        result.FilterReason = $"Filtered by branch: {targetBranchId}";

        _logger.LogInformation(
            "🏢 [Branch Filter] Filtering completed. " +
            "TotalProducts={TotalProducts}, ProductsInTargetBranch={ProductsInTargetBranch}, " +
            "ProductsWithoutBranches={ProductsWithoutBranches} (EXCLUDED), " +
            "ProductsNotInTargetBranch={ProductsNotInTargetBranch} (EXCLUDED), " +
            "TargetBranch={TargetBranch}, CorrelationId={CorrelationId}",
            result.TotalProducts, result.FilteredCount, result.ProductsWithoutBranches,
            result.ProductsNotInTargetBranch, targetBranchId, correlationId);

        return result;
    }

    /// <summary>
    /// Validates that a branch ID exists and has products available.
    /// </summary>
    /// <param name="allProducts">All products from Foodics</param>
    /// <param name="branchId">Branch ID to validate</param>
    /// <returns>Validation result</returns>
    public BranchValidationResult ValidateBranch(
        Dictionary<string, FoodicsProductDetailDto> allProducts,
        string branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId))
        {
            return new BranchValidationResult
            {
                IsValid = false,
                Message = "Branch ID is null or empty"
            };
        }

        var productsInBranch = allProducts.Values
            .Where(p => p.Branches != null && 
                       p.Branches.Any(b => string.Equals(b.Id, branchId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var branchExists = productsInBranch.Any();
        
        return new BranchValidationResult
        {
            IsValid = branchExists,
            ProductCount = productsInBranch.Count,
            Message = branchExists 
                ? $"Branch {branchId} found with {productsInBranch.Count} products"
                : $"Branch {branchId} not found or has no products"
        };
    }
}

/// <summary>
/// Result of product filtering by branch
/// </summary>
public class FilteredProductsResult
{
    public List<FoodicsProductDetailDto> FilteredProducts { get; set; } = new();
    public int TotalProducts { get; set; }
    public int FilteredCount { get; set; }
    public int ProductsWithoutBranches { get; set; }
    public int ProductsNotInTargetBranch { get; set; }
    public string FilterReason { get; set; } = string.Empty;
}

/// <summary>
/// Result of branch validation
/// </summary>
public class BranchValidationResult
{
    public bool IsValid { get; set; }
    public int ProductCount { get; set; }
    public string Message { get; set; } = string.Empty;
}