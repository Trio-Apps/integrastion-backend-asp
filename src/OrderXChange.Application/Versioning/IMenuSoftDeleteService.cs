using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service interface for managing soft deletion of menu items
/// Handles the complete lifecycle of item deletion with audit trail
/// </summary>
public interface IMenuSoftDeleteService
{
    /// <summary>
    /// Processes soft deletion of menu items that are no longer available in Foodics
    /// Compares current menu with previous snapshot to identify deleted items
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="currentProducts">Current products from Foodics API</param>
    /// <param name="previousProducts">Previous products from last sync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of processed deletions</returns>
    Task<List<MenuItemDeletion>> ProcessDeletedItemsAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        List<FoodicsProductDetailDto> previousProducts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually marks a menu item as soft deleted
    /// Used for administrative actions or compliance requirements
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="entityType">Type of entity (Product, Category, etc.)</param>
    /// <param name="entityId">Entity ID from Foodics</param>
    /// <param name="reason">Reason for deletion</param>
    /// <param name="deletedBy">User/system performing the deletion</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created deletion record</returns>
    Task<MenuItemDeletion> SoftDeleteItemAsync(
        Guid foodicsAccountId,
        string? branchId,
        string entityType,
        string entityId,
        string reason,
        string deletedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs pending deletions to Talabat
    /// Handles batch processing and partial failures
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="talabatVendorCode">Target Talabat vendor code</param>
    /// <param name="maxBatchSize">Maximum number of deletions to process in one batch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync result with statistics</returns>
    Task<DeletionSyncResult> SyncDeletionsToTalabatAsync(
        Guid foodicsAccountId,
        string talabatVendorCode,
        int maxBatchSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft deleted item (if possible)
    /// Validates that the item still exists in Foodics before restoration
    /// </summary>
    /// <param name="deletionId">Deletion record ID</param>
    /// <param name="restoredBy">User/system performing the restoration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Restoration result</returns>
    Task<ItemRestorationResult> RestoreDeletedItemAsync(
        Guid deletionId,
        string restoredBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending deletions that need to be synced to Talabat
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending deletions</returns>
    Task<List<MenuItemDeletion>> GetPendingDeletionsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deletion history for a specific entity
    /// </summary>
    /// <param name="entityType">Entity type</param>
    /// <param name="entityId">Entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deletion history</returns>
    Task<List<MenuItemDeletion>> GetDeletionHistoryAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired deletion records
    /// Removes old deletion records that are past their retention period
    /// </summary>
    /// <param name="retentionDays">Number of days to retain deletion records</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cleanup statistics</returns>
    Task<DeletionCleanupResult> CleanupExpiredDeletionsAsync(
        int retentionDays = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if an item can be safely deleted
    /// Checks for dependencies and business rules
    /// </summary>
    /// <param name="entityType">Entity type</param>
    /// <param name="entityId">Entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<DeletionValidationResult> ValidateDeletionAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of deletion sync operation
/// </summary>
public class DeletionSyncResult
{
    public bool Success { get; set; }
    public int ProcessedDeletions { get; set; }
    public int FailedDeletions { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public TimeSpan SyncTime { get; set; }
}

/// <summary>
/// Result of item restoration operation
/// </summary>
public class ItemRestorationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ItemExistsInFoodics { get; set; }
    public bool RequiresTalabatSync { get; set; }
}

/// <summary>
/// Result of deletion cleanup operation
/// </summary>
public class DeletionCleanupResult
{
    public int DeletedRecords { get; set; }
    public long FreedStorageBytes { get; set; }
    public TimeSpan CleanupTime { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of deletion validation
/// </summary>
public class DeletionValidationResult
{
    public bool CanDelete { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
}