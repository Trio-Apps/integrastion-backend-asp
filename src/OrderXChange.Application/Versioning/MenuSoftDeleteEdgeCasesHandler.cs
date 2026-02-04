using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Handles edge cases and complex scenarios in menu soft delete lifecycle
/// Provides specialized logic for handling unusual deletion scenarios
/// </summary>
public class MenuSoftDeleteEdgeCasesHandler : ITransientDependency
{
    private readonly IMenuSoftDeleteService _softDeleteService;
    private readonly ILogger<MenuSoftDeleteEdgeCasesHandler> _logger;

    public MenuSoftDeleteEdgeCasesHandler(
        IMenuSoftDeleteService softDeleteService,
        ILogger<MenuSoftDeleteEdgeCasesHandler> logger)
    {
        _softDeleteService = softDeleteService;
        _logger = logger;
    }

    /// <summary>
    /// Handles cascade deletion when a category is deleted
    /// All products in the category should be soft deleted
    /// </summary>
    public async Task HandleCategoryDeletionCascadeAsync(
        Guid foodicsAccountId,
        string? branchId,
        string categoryId,
        string categoryName,
        List<FoodicsProductDetailDto> affectedProducts,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling category deletion cascade. CategoryId={CategoryId}, AffectedProducts={Count}",
            categoryId, affectedProducts.Count);

        // First, soft delete the category itself
        await _softDeleteService.SoftDeleteItemAsync(
            foodicsAccountId,
            branchId,
            MenuEntityType.Category,
            categoryId,
            MenuDeletionReason.CategoryDeleted,
            "System",
            cancellationToken);

        // Then, soft delete all products in this category
        foreach (var product in affectedProducts)
        {
            try
            {
                await _softDeleteService.SoftDeleteItemAsync(
                    foodicsAccountId,
                    branchId,
                    MenuEntityType.Product,
                    product.Id,
                    MenuDeletionReason.CategoryDeleted,
                    "System",
                    cancellationToken);

                _logger.LogDebug(
                    "Cascade deleted product {ProductId} due to category deletion",
                    product.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to cascade delete product {ProductId} for category {CategoryId}",
                    product.Id, categoryId);
            }
        }

        _logger.LogInformation(
            "Category deletion cascade completed. CategoryId={CategoryId}, ProcessedProducts={Count}",
            categoryId, affectedProducts.Count);
    }

    /// <summary>
    /// Handles temporary unavailability (different from permanent deletion)
    /// Items are marked as temporarily unavailable but can be easily restored
    /// </summary>
    public async Task HandleTemporaryUnavailabilityAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<string> productIds,
        string reason,
        TimeSpan unavailabilityDuration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling temporary unavailability. ProductCount={Count}, Duration={Duration}",
            productIds.Count, unavailabilityDuration);

        foreach (var productId in productIds)
        {
            try
            {
                var deletion = await _softDeleteService.SoftDeleteItemAsync(
                    foodicsAccountId,
                    branchId,
                    MenuEntityType.Product,
                    productId,
                    MenuDeletionReason.TemporaryUnavailable,
                    "System",
                    cancellationToken);

                // Set shorter expiry for temporary unavailability
                // This will be handled by a background job to restore items
                // TODO: Update deletion record with custom expiry
                
                _logger.LogDebug(
                    "Marked product {ProductId} as temporarily unavailable",
                    productId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to mark product {ProductId} as temporarily unavailable",
                    productId);
            }
        }
    }

    /// <summary>
    /// Handles branch closure scenario
    /// All products specific to a branch should be soft deleted
    /// </summary>
    public async Task HandleBranchClosureAsync(
        Guid foodicsAccountId,
        string branchId,
        List<FoodicsProductDetailDto> branchProducts,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling branch closure. BranchId={BranchId}, ProductCount={Count}",
            branchId, branchProducts.Count);

        foreach (var product in branchProducts)
        {
            try
            {
                await _softDeleteService.SoftDeleteItemAsync(
                    foodicsAccountId,
                    branchId,
                    MenuEntityType.Product,
                    product.Id,
                    MenuDeletionReason.BranchUnavailable,
                    "System",
                    cancellationToken);

                _logger.LogDebug(
                    "Soft deleted product {ProductId} due to branch closure",
                    product.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to soft delete product {ProductId} for branch closure",
                    product.Id);
            }
        }

        _logger.LogInformation(
            "Branch closure handling completed. BranchId={BranchId}, ProcessedProducts={Count}",
            branchId, branchProducts.Count);
    }

    /// <summary>
    /// Handles duplicate deletion attempts
    /// Prevents duplicate deletion records and handles race conditions
    /// </summary>
    public async Task<bool> HandleDuplicateDeletionAsync(
        Guid foodicsAccountId,
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Checking for duplicate deletion. EntityType={EntityType}, EntityId={EntityId}",
            entityType, entityId);

        var existingDeletions = await _softDeleteService.GetDeletionHistoryAsync(
            entityType, entityId, cancellationToken);

        var activeDeletion = existingDeletions
            .Where(d => d.CanRollback && !d.ExpiresAt.HasValue || d.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(d => d.CreationTime)
            .FirstOrDefault();

        if (activeDeletion != null)
        {
            _logger.LogInformation(
                "Duplicate deletion detected. EntityType={EntityType}, EntityId={EntityId}, ExistingDeletionId={DeletionId}",
                entityType, entityId, activeDeletion.Id);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles partial sync failures for deletions
    /// Retries failed deletions with exponential backoff
    /// </summary>
    public async Task HandlePartialSyncFailuresAsync(
        Guid foodicsAccountId,
        string talabatVendorCode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling partial sync failures. AccountId={AccountId}, VendorCode={VendorCode}",
            foodicsAccountId, talabatVendorCode);

        var pendingDeletions = await _softDeleteService.GetPendingDeletionsAsync(
            foodicsAccountId, cancellationToken: cancellationToken);

        var failedDeletions = pendingDeletions
            .Where(d => d.TalabatSyncStatus == MenuDeletionSyncStatus.Failed)
            .Where(d => d.TalabatSyncRetryCount < 5) // Max 5 retries
            .OrderBy(d => d.TalabatSyncRetryCount)
            .ThenBy(d => d.CreationTime)
            .Take(10) // Process max 10 failed deletions at a time
            .ToList();

        if (!failedDeletions.Any())
        {
            _logger.LogInformation("No failed deletions to retry");
            return;
        }

        foreach (var deletion in failedDeletions)
        {
            try
            {
                // Calculate exponential backoff delay
                var backoffDelay = TimeSpan.FromSeconds(Math.Pow(2, deletion.TalabatSyncRetryCount));
                if (backoffDelay > TimeSpan.FromMinutes(30))
                {
                    backoffDelay = TimeSpan.FromMinutes(30); // Max 30 minutes
                }

                _logger.LogInformation(
                    "Retrying deletion sync after {Delay}s. DeletionId={DeletionId}, RetryCount={RetryCount}",
                    backoffDelay.TotalSeconds, deletion.Id, deletion.TalabatSyncRetryCount);

                await Task.Delay(backoffDelay, cancellationToken);

                // Retry the sync (this would call the actual Talabat API)
                var syncResult = await _softDeleteService.SyncDeletionsToTalabatAsync(
                    foodicsAccountId, talabatVendorCode, 1, cancellationToken);

                if (syncResult.Success)
                {
                    _logger.LogInformation(
                        "Deletion sync retry successful. DeletionId={DeletionId}",
                        deletion.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Deletion sync retry failed. DeletionId={DeletionId}, Errors={Errors}",
                        deletion.Id, string.Join(", ", syncResult.Errors));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception during deletion sync retry. DeletionId={DeletionId}",
                    deletion.Id);
            }
        }
    }

    /// <summary>
    /// Handles orphaned products (products without valid categories)
    /// Decides whether to soft delete or reassign to default category
    /// </summary>
    public async Task HandleOrphanedProductsAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> orphanedProducts,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling orphaned products. Count={Count}",
            orphanedProducts.Count);

        foreach (var product in orphanedProducts)
        {
            try
            {
                // Check if the product's category was recently deleted
                var categoryDeletions = await _softDeleteService.GetDeletionHistoryAsync(
                    MenuEntityType.Category, product.CategoryId ?? "", cancellationToken);

                if (categoryDeletions.Any(d => d.CreationTime > DateTime.UtcNow.AddHours(-24)))
                {
                    // Category was recently deleted, soft delete the product
                    await _softDeleteService.SoftDeleteItemAsync(
                        foodicsAccountId,
                        branchId,
                        MenuEntityType.Product,
                        product.Id,
                        MenuDeletionReason.CategoryDeleted,
                        "System",
                        cancellationToken);

                    _logger.LogInformation(
                        "Soft deleted orphaned product {ProductId} due to category deletion",
                        product.Id);
                }
                else
                {
                    // Category might be temporarily unavailable, just log a warning
                    _logger.LogWarning(
                        "Product {ProductId} has invalid category {CategoryId} but category wasn't recently deleted",
                        product.Id, product.CategoryId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to handle orphaned product {ProductId}",
                    product.Id);
            }
        }
    }

    /// <summary>
    /// Handles bulk restoration of temporarily unavailable items
    /// Used when items become available again after temporary unavailability
    /// </summary>
    public async Task HandleBulkRestorationAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<string> productIds,
        string restoredBy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling bulk restoration. ProductCount={Count}, RestoredBy={RestoredBy}",
            productIds.Count, restoredBy);

        var restorationResults = new List<(string ProductId, bool Success, string? Error)>();

        foreach (var productId in productIds)
        {
            try
            {
                // Find the most recent deletion for this product
                var deletionHistory = await _softDeleteService.GetDeletionHistoryAsync(
                    MenuEntityType.Product, productId, cancellationToken);

                var latestDeletion = deletionHistory
                    .Where(d => d.CanRollback)
                    .OrderByDescending(d => d.CreationTime)
                    .FirstOrDefault();

                if (latestDeletion != null)
                {
                    var result = await _softDeleteService.RestoreDeletedItemAsync(
                        latestDeletion.Id, restoredBy, cancellationToken);

                    restorationResults.Add((productId, result.Success, result.ErrorMessage));

                    if (result.Success)
                    {
                        _logger.LogDebug("Successfully restored product {ProductId}", productId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to restore product {ProductId}: {Error}", 
                            productId, result.ErrorMessage);
                    }
                }
                else
                {
                    restorationResults.Add((productId, false, "No restorable deletion found"));
                    _logger.LogWarning("No restorable deletion found for product {ProductId}", productId);
                }
            }
            catch (Exception ex)
            {
                restorationResults.Add((productId, false, ex.Message));
                _logger.LogError(ex, "Exception during restoration of product {ProductId}", productId);
            }
        }

        var successCount = restorationResults.Count(r => r.Success);
        var failureCount = restorationResults.Count(r => !r.Success);

        _logger.LogInformation(
            "Bulk restoration completed. Successful={Success}, Failed={Failed}",
            successCount, failureCount);
    }
}