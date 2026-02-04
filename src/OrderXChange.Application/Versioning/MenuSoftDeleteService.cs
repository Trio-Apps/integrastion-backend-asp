using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Staging;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Implementation of menu soft delete service
/// Manages the complete lifecycle of menu item deletion with audit trail
/// </summary>
public class MenuSoftDeleteService : IMenuSoftDeleteService, ITransientDependency
{
    private readonly IRepository<MenuItemDeletion, Guid> _deletionRepository;
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
    private readonly IRepository<MenuChangeLog, Guid> _changeLogRepository;
    private readonly ILogger<MenuSoftDeleteService> _logger;

    public MenuSoftDeleteService(
        IRepository<MenuItemDeletion, Guid> deletionRepository,
        IRepository<FoodicsProductStaging, Guid> stagingRepository,
        IRepository<MenuChangeLog, Guid> changeLogRepository,
        ILogger<MenuSoftDeleteService> logger)
    {
        _deletionRepository = deletionRepository;
        _stagingRepository = stagingRepository;
        _changeLogRepository = changeLogRepository;
        _logger = logger;
    }

    public async Task<List<MenuItemDeletion>> ProcessDeletedItemsAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        List<FoodicsProductDetailDto> previousProducts,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing deleted items. AccountId={AccountId}, BranchId={BranchId}, Current={Current}, Previous={Previous}",
            foodicsAccountId, branchId ?? "ALL", currentProducts.Count, previousProducts.Count);

        var deletions = new List<MenuItemDeletion>();

        // Create lookup dictionaries
        var currentDict = currentProducts.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var previousDict = previousProducts.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

        // Find products that existed before but don't exist now
        var deletedProductIds = previousDict.Keys.Except(currentDict.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var productId in deletedProductIds)
        {
            var previousProduct = previousDict[productId];
            
            // Check if this deletion already exists
            var existingDeletion = await _deletionRepository.FirstOrDefaultAsync(
                d => d.FoodicsAccountId == foodicsAccountId &&
                     d.BranchId == branchId &&
                     d.EntityType == MenuEntityType.Product &&
                     d.EntityId == productId,
                cancellationToken: cancellationToken);

            if (existingDeletion != null)
            {
                _logger.LogDebug("Deletion already exists for product {ProductId}", productId);
                continue;
            }

            // Create deletion record
            var deletion = new MenuItemDeletion
            {
                FoodicsAccountId = foodicsAccountId,
                BranchId = branchId,
                EntityType = MenuEntityType.Product,
                EntityId = productId,
                EntityName = previousProduct.Name,
                DeletionReason = MenuDeletionReason.RemovedFromFoodics,
                DeletionSource = MenuDeletionSource.FoodicsSync,
                ProcessedAt = DateTime.UtcNow,
                EntitySnapshotJson = JsonSerializer.Serialize(new
                {
                    previousProduct.Id,
                    previousProduct.Name,
                    previousProduct.Price,
                    previousProduct.IsActive,
                    previousProduct.CategoryId,
                    CategoryName = previousProduct.Category?.Name,
                    previousProduct.Description
                }),
                ExpiresAt = DateTime.UtcNow.AddDays(90), // 90 days retention
                CanRollback = true
            };

            await _deletionRepository.InsertAsync(deletion, autoSave: false, cancellationToken: cancellationToken);
            deletions.Add(deletion);

            // Mark staging record as soft deleted
            await MarkStagingItemAsDeletedAsync(foodicsAccountId, productId, deletion.Id, cancellationToken);

            _logger.LogInformation(
                "Created deletion record. ProductId={ProductId}, ProductName={ProductName}, DeletionId={DeletionId}",
                productId, previousProduct.Name, deletion.Id);
        }

        // Process category deletions
        await ProcessDeletedCategoriesAsync(foodicsAccountId, branchId, currentProducts, previousProducts, deletions, cancellationToken);

        // Process modifier deletions
        await ProcessDeletedModifiersAsync(foodicsAccountId, branchId, currentProducts, previousProducts, deletions, cancellationToken);

        if (deletions.Any())
        {
            await _deletionRepository.GetDbContext().SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation(
                "Processed {Count} deletions. AccountId={AccountId}, BranchId={BranchId}",
                deletions.Count, foodicsAccountId, branchId ?? "ALL");
        }

        return deletions;
    }

    public async Task<MenuItemDeletion> SoftDeleteItemAsync(
        Guid foodicsAccountId,
        string? branchId,
        string entityType,
        string entityId,
        string reason,
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Manual soft delete. AccountId={AccountId}, EntityType={EntityType}, EntityId={EntityId}, Reason={Reason}",
            foodicsAccountId, entityType, entityId, reason);

        // Validate deletion
        var validation = await ValidateDeletionAsync(entityType, entityId, cancellationToken);
        if (!validation.CanDelete)
        {
            throw new InvalidOperationException($"Cannot delete {entityType} {entityId}: {string.Join(", ", validation.BlockingIssues)}");
        }

        // Get entity snapshot
        var entitySnapshot = await GetEntitySnapshotAsync(foodicsAccountId, entityType, entityId, cancellationToken);

        var deletion = new MenuItemDeletion
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entitySnapshot?.Name,
            DeletionReason = reason,
            DeletionSource = MenuDeletionSource.ManualAdmin,
            ProcessedAt = DateTime.UtcNow,
            EntitySnapshotJson = entitySnapshot != null ? JsonSerializer.Serialize(entitySnapshot) : null,
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            CanRollback = true
        };

        await _deletionRepository.InsertAsync(deletion, autoSave: true, cancellationToken: cancellationToken);

        // Mark staging record as soft deleted if it's a product
        if (entityType == MenuEntityType.Product)
        {
            await MarkStagingItemAsDeletedAsync(foodicsAccountId, entityId, deletion.Id, cancellationToken);
        }

        _logger.LogInformation(
            "Manual deletion created. DeletionId={DeletionId}, DeletedBy={DeletedBy}",
            deletion.Id, deletedBy);

        return deletion;
    }

    public async Task<DeletionSyncResult> SyncDeletionsToTalabatAsync(
        Guid foodicsAccountId,
        string talabatVendorCode,
        int maxBatchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DeletionSyncResult { Success = true };

        try
        {
            _logger.LogInformation(
                "Syncing deletions to Talabat. AccountId={AccountId}, VendorCode={VendorCode}, BatchSize={BatchSize}",
                foodicsAccountId, talabatVendorCode, maxBatchSize);

            // Get pending deletions
            var pendingDeletions = await GetPendingDeletionsAsync(foodicsAccountId, cancellationToken: cancellationToken);
            var batchDeletions = pendingDeletions.Take(maxBatchSize).ToList();

            if (!batchDeletions.Any())
            {
                _logger.LogInformation("No pending deletions to sync");
                result.SyncTime = stopwatch.Elapsed;
                return result;
            }

            // Group by entity type for efficient processing
            var productDeletions = batchDeletions.Where(d => d.EntityType == MenuEntityType.Product).ToList();
            var categoryDeletions = batchDeletions.Where(d => d.EntityType == MenuEntityType.Category).ToList();
            var modifierDeletions = batchDeletions.Where(d => d.EntityType == MenuEntityType.Modifier).ToList();

            // Sync product deletions
            if (productDeletions.Any())
            {
                var productSyncResult = await SyncProductDeletionsToTalabatAsync(
                    productDeletions, talabatVendorCode, cancellationToken);
                
                result.ProcessedDeletions += productSyncResult.ProcessedDeletions;
                result.FailedDeletions += productSyncResult.FailedDeletions;
                result.Errors.AddRange(productSyncResult.Errors);
            }

            // Sync category deletions
            if (categoryDeletions.Any())
            {
                var categorySyncResult = await SyncCategoryDeletionsToTalabatAsync(
                    categoryDeletions, talabatVendorCode, cancellationToken);
                
                result.ProcessedDeletions += categorySyncResult.ProcessedDeletions;
                result.FailedDeletions += categorySyncResult.FailedDeletions;
                result.Errors.AddRange(categorySyncResult.Errors);
            }

            // Update sync status for successful deletions
            var successfulDeletions = batchDeletions
                .Where(d => !result.Errors.Any(e => e.Contains(d.EntityId)))
                .ToList();

            foreach (var deletion in successfulDeletions)
            {
                deletion.IsSyncedToTalabat = true;
                deletion.TalabatVendorCode = talabatVendorCode;
                deletion.TalabatSyncedAt = DateTime.UtcNow;
                deletion.TalabatSyncStatus = MenuDeletionSyncStatus.Completed;
            }

            // Update failed deletions
            var failedDeletions = batchDeletions.Except(successfulDeletions).ToList();
            foreach (var deletion in failedDeletions)
            {
                deletion.TalabatSyncStatus = MenuDeletionSyncStatus.Failed;
                deletion.TalabatSyncRetryCount++;
                deletion.TalabatSyncError = result.Errors.FirstOrDefault(e => e.Contains(deletion.EntityId));
            }

            await _deletionRepository.UpdateManyAsync(batchDeletions, autoSave: true, cancellationToken: cancellationToken);

            result.Success = result.FailedDeletions == 0;
            stopwatch.Stop();
            result.SyncTime = stopwatch.Elapsed;

            _logger.LogInformation(
                "Deletion sync completed. Processed={Processed}, Failed={Failed}, Time={Time}ms",
                result.ProcessedDeletions, result.FailedDeletions, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync deletions to Talabat");
            
            return new DeletionSyncResult
            {
                Success = false,
                Errors = { ex.Message },
                SyncTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<ItemRestorationResult> RestoreDeletedItemAsync(
        Guid deletionId,
        string restoredBy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Restoring deleted item. DeletionId={DeletionId}, RestoredBy={RestoredBy}", deletionId, restoredBy);

        var deletion = await _deletionRepository.GetAsync(deletionId, cancellationToken: cancellationToken);

        if (!deletion.CanRollback)
        {
            return new ItemRestorationResult
            {
                Success = false,
                ErrorMessage = "Item cannot be rolled back"
            };
        }

        // Check if item exists in Foodics (for validation)
        var existsInFoodics = await CheckItemExistsInFoodicsAsync(deletion.EntityType, deletion.EntityId, cancellationToken);

        if (!existsInFoodics)
        {
            return new ItemRestorationResult
            {
                Success = false,
                ErrorMessage = "Item no longer exists in Foodics",
                ItemExistsInFoodics = false
            };
        }

        // Restore staging record if it's a product
        if (deletion.EntityType == MenuEntityType.Product)
        {
            await RestoreStagingItemAsync(deletion.FoodicsAccountId, deletion.EntityId, cancellationToken);
        }

        // Mark deletion as restored (soft delete the deletion record)
        deletion.CanRollback = false;
        deletion.ExpiresAt = DateTime.UtcNow; // Mark for immediate cleanup

        await _deletionRepository.UpdateAsync(deletion, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation("Item restored successfully. DeletionId={DeletionId}", deletionId);

        return new ItemRestorationResult
        {
            Success = true,
            ItemExistsInFoodics = true,
            RequiresTalabatSync = deletion.IsSyncedToTalabat
        };
    }

    public async Task<List<MenuItemDeletion>> GetPendingDeletionsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = await _deletionRepository.GetQueryableAsync();

        return await query
            .Where(d => d.FoodicsAccountId == foodicsAccountId)
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => !d.IsSyncedToTalabat)
            .Where(d => d.TalabatSyncStatus != MenuDeletionSyncStatus.Completed)
            .OrderBy(d => d.CreationTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MenuItemDeletion>> GetDeletionHistoryAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        return await _deletionRepository.GetListAsync(
            d => d.EntityType == entityType && d.EntityId == entityId,
            cancellationToken: cancellationToken);
    }

    public async Task<DeletionCleanupResult> CleanupExpiredDeletionsAsync(
        int retentionDays = 90,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var query = await _deletionRepository.GetQueryableAsync();
            
            var expiredDeletions = await query
                .Where(d => d.ExpiresAt.HasValue && d.ExpiresAt.Value < DateTime.UtcNow)
                .Where(d => !d.CanRollback || d.CreationTime < cutoffDate)
                .ToListAsync(cancellationToken);

            var freedBytes = expiredDeletions.Sum(d => 
                (d.EntitySnapshotJson?.Length ?? 0) + 
                (d.AffectedEntitiesJson?.Length ?? 0) + 
                (d.TalabatSyncError?.Length ?? 0));

            await _deletionRepository.DeleteManyAsync(expiredDeletions, autoSave: true, cancellationToken: cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Cleaned up {Count} expired deletions, freed {Size}KB in {Time}ms",
                expiredDeletions.Count, freedBytes / 1024, stopwatch.ElapsedMilliseconds);

            return new DeletionCleanupResult
            {
                DeletedRecords = expiredDeletions.Count,
                FreedStorageBytes = freedBytes,
                CleanupTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired deletions");
            
            return new DeletionCleanupResult
            {
                CleanupTime = stopwatch.Elapsed,
                Errors = { ex.Message }
            };
        }
    }

    public async Task<DeletionValidationResult> ValidateDeletionAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var result = new DeletionValidationResult { CanDelete = true };

        // Check if already deleted
        var existingDeletion = await _deletionRepository.FirstOrDefaultAsync(
            d => d.EntityType == entityType && d.EntityId == entityId,
            cancellationToken: cancellationToken);

        if (existingDeletion != null)
        {
            result.CanDelete = false;
            result.BlockingIssues.Add("Item is already marked as deleted");
            return result;
        }

        // Entity-specific validation
        switch (entityType)
        {
            case MenuEntityType.Category:
                await ValidateCategoryDeletionAsync(entityId, result, cancellationToken);
                break;
            case MenuEntityType.Modifier:
                await ValidateModifierDeletionAsync(entityId, result, cancellationToken);
                break;
        }

        return result;
    }

    #region Private Methods

    private async Task ProcessDeletedCategoriesAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        List<FoodicsProductDetailDto> previousProducts,
        List<MenuItemDeletion> deletions,
        CancellationToken cancellationToken)
    {
        var currentCategories = currentProducts
            .Where(p => p.Category != null)
            .Select(p => p.Category!)
            .DistinctBy(c => c.Id)
            .ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

        var previousCategories = previousProducts
            .Where(p => p.Category != null)
            .Select(p => p.Category!)
            .DistinctBy(c => c.Id)
            .ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

        var deletedCategoryIds = previousCategories.Keys.Except(currentCategories.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var categoryId in deletedCategoryIds)
        {
            var previousCategory = previousCategories[categoryId];
            
            var deletion = new MenuItemDeletion
            {
                FoodicsAccountId = foodicsAccountId,
                BranchId = branchId,
                EntityType = MenuEntityType.Category,
                EntityId = categoryId,
                EntityName = previousCategory.Name,
                DeletionReason = MenuDeletionReason.RemovedFromFoodics,
                DeletionSource = MenuDeletionSource.FoodicsSync,
                ProcessedAt = DateTime.UtcNow,
                EntitySnapshotJson = JsonSerializer.Serialize(previousCategory),
                ExpiresAt = DateTime.UtcNow.AddDays(90),
                CanRollback = true
            };

            await _deletionRepository.InsertAsync(deletion, autoSave: false, cancellationToken: cancellationToken);
            deletions.Add(deletion);
        }
    }

    private async Task ProcessDeletedModifiersAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        List<FoodicsProductDetailDto> previousProducts,
        List<MenuItemDeletion> deletions,
        CancellationToken cancellationToken)
    {
        var currentModifiers = currentProducts
            .Where(p => p.Modifiers != null)
            .SelectMany(p => p.Modifiers!)
            .DistinctBy(m => m.Id)
            .ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        var previousModifiers = previousProducts
            .Where(p => p.Modifiers != null)
            .SelectMany(p => p.Modifiers!)
            .DistinctBy(m => m.Id)
            .ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        var deletedModifierIds = previousModifiers.Keys.Except(currentModifiers.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var modifierId in deletedModifierIds)
        {
            var previousModifier = previousModifiers[modifierId];
            
            var deletion = new MenuItemDeletion
            {
                FoodicsAccountId = foodicsAccountId,
                BranchId = branchId,
                EntityType = MenuEntityType.Modifier,
                EntityId = modifierId,
                EntityName = previousModifier.Name,
                DeletionReason = MenuDeletionReason.RemovedFromFoodics,
                DeletionSource = MenuDeletionSource.FoodicsSync,
                ProcessedAt = DateTime.UtcNow,
                EntitySnapshotJson = JsonSerializer.Serialize(previousModifier),
                ExpiresAt = DateTime.UtcNow.AddDays(90),
                CanRollback = true
            };

            await _deletionRepository.InsertAsync(deletion, autoSave: false, cancellationToken: cancellationToken);
            deletions.Add(deletion);
        }
    }

    private async Task MarkStagingItemAsDeletedAsync(
        Guid foodicsAccountId,
        string productId,
        Guid deletionId,
        CancellationToken cancellationToken)
    {
        var stagingItem = await _stagingRepository.FirstOrDefaultAsync(
            s => s.FoodicsAccountId == foodicsAccountId && s.FoodicsProductId == productId,
            cancellationToken: cancellationToken);

        if (stagingItem != null)
        {
            stagingItem.IsDeleted = true;
            stagingItem.DeletedAt = DateTime.UtcNow;
            stagingItem.DeletionReason = MenuDeletionReason.RemovedFromFoodics;
            stagingItem.DeletedBy = "System";
            
            await _stagingRepository.UpdateAsync(stagingItem, autoSave: false, cancellationToken: cancellationToken);
        }
    }

    private async Task RestoreStagingItemAsync(
        Guid foodicsAccountId,
        string productId,
        CancellationToken cancellationToken)
    {
        var stagingItem = await _stagingRepository.FirstOrDefaultAsync(
            s => s.FoodicsAccountId == foodicsAccountId && s.FoodicsProductId == productId,
            cancellationToken: cancellationToken);

        if (stagingItem != null)
        {
            stagingItem.IsDeleted = false;
            stagingItem.DeletedAt = null;
            stagingItem.DeletionReason = null;
            stagingItem.DeletedBy = null;
            stagingItem.IsDeletionSyncedToTalabat = false;
            stagingItem.DeletionSyncedAt = null;
            stagingItem.DeletionSyncError = null;
            
            await _stagingRepository.UpdateAsync(stagingItem, autoSave: true, cancellationToken: cancellationToken);
        }
    }

    private async Task<DeletionSyncResult> SyncProductDeletionsToTalabatAsync(
        List<MenuItemDeletion> productDeletions,
        string talabatVendorCode,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual Talabat API integration for product deletions
        // This is a placeholder implementation
        
        _logger.LogInformation(
            "Syncing {Count} product deletions to Talabat. VendorCode={VendorCode}",
            productDeletions.Count, talabatVendorCode);

        // Simulate API call delay
        await Task.Delay(500, cancellationToken);

        // Simulate success for now
        return new DeletionSyncResult
        {
            Success = true,
            ProcessedDeletions = productDeletions.Count,
            FailedDeletions = 0
        };
    }

    private async Task<DeletionSyncResult> SyncCategoryDeletionsToTalabatAsync(
        List<MenuItemDeletion> categoryDeletions,
        string talabatVendorCode,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual Talabat API integration for category deletions
        // This is a placeholder implementation
        
        _logger.LogInformation(
            "Syncing {Count} category deletions to Talabat. VendorCode={VendorCode}",
            categoryDeletions.Count, talabatVendorCode);

        // Simulate API call delay
        await Task.Delay(300, cancellationToken);

        // Simulate success for now
        return new DeletionSyncResult
        {
            Success = true,
            ProcessedDeletions = categoryDeletions.Count,
            FailedDeletions = 0
        };
    }

    private async Task<bool> CheckItemExistsInFoodicsAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual Foodics API check
        // This is a placeholder implementation
        await Task.Delay(100, cancellationToken);
        return true; // Assume exists for now
    }

    private async Task<dynamic?> GetEntitySnapshotAsync(
        Guid foodicsAccountId,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        if (entityType == MenuEntityType.Product)
        {
            var stagingItem = await _stagingRepository.FirstOrDefaultAsync(
                s => s.FoodicsAccountId == foodicsAccountId && s.FoodicsProductId == entityId,
                cancellationToken: cancellationToken);

            if (stagingItem != null)
            {
                return new
                {
                    Id = stagingItem.FoodicsProductId,
                    Name = stagingItem.Name,
                    Price = stagingItem.Price,
                    IsActive = stagingItem.IsActive,
                    CategoryId = stagingItem.CategoryId,
                    CategoryName = stagingItem.CategoryName,
                    Description = stagingItem.Description
                };
            }
        }

        return null;
    }

    private async Task ValidateCategoryDeletionAsync(
        string categoryId,
        DeletionValidationResult result,
        CancellationToken cancellationToken)
    {
        // Check if any products still use this category
        var productsUsingCategory = await _stagingRepository.CountAsync(
            s => s.CategoryId == categoryId && !s.IsDeleted,
            cancellationToken: cancellationToken);

        if (productsUsingCategory > 0)
        {
            result.Warnings.Add($"Category has {productsUsingCategory} active products");
            result.Dependencies.Add($"{productsUsingCategory} products");
        }
    }

    private async Task ValidateModifierDeletionAsync(
        string modifierId,
        DeletionValidationResult result,
        CancellationToken cancellationToken)
    {
        // Check if any products still use this modifier
        // This would require parsing ModifiersJson in staging records
        // For now, just add a warning
        result.Warnings.Add("Modifier deletion may affect products");
    }

    #endregion
}