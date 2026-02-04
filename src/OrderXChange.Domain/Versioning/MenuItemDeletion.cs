using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Tracks soft deletion of menu items with full audit trail
/// Maintains history of deleted items for compliance and rollback capabilities
/// </summary>
public class MenuItemDeletion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    [Required]
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID where the item was deleted (null for all branches)
    /// </summary>
    [MaxLength(100)]
    public string? BranchId { get; set; }

    /// <summary>
    /// Type of deleted entity: Product, Category, Modifier, ModifierOption
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Entity ID from Foodics
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Entity name at time of deletion (for reference)
    /// </summary>
    [MaxLength(500)]
    public string? EntityName { get; set; }

    /// <summary>
    /// Reason for deletion
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DeletionReason { get; set; } = string.Empty;

    /// <summary>
    /// Source of deletion: FoodicsSync, ManualAdmin, BranchClosure, etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DeletionSource { get; set; } = string.Empty;

    /// <summary>
    /// When the item was deleted from Foodics (if applicable)
    /// </summary>
    public DateTime? FoodicsDeletedAt { get; set; }

    /// <summary>
    /// When we detected/processed the deletion
    /// </summary>
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// JSON snapshot of the entity at time of deletion (for rollback)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? EntitySnapshotJson { get; set; }

    /// <summary>
    /// Related entities that were affected by this deletion
    /// JSON array of {type, id, name} objects
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? AffectedEntitiesJson { get; set; }

    /// <summary>
    /// Whether this deletion has been synced to Talabat
    /// </summary>
    public bool IsSyncedToTalabat { get; set; }

    /// <summary>
    /// Talabat vendor code where deletion was synced
    /// </summary>
    [MaxLength(100)]
    public string? TalabatVendorCode { get; set; }

    /// <summary>
    /// When the deletion was synced to Talabat
    /// </summary>
    public DateTime? TalabatSyncedAt { get; set; }

    /// <summary>
    /// Talabat sync status: Pending, InProgress, Completed, Failed
    /// </summary>
    [MaxLength(50)]
    public string TalabatSyncStatus { get; set; } = MenuDeletionSyncStatus.Pending;

    /// <summary>
    /// Error details if Talabat sync failed
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? TalabatSyncError { get; set; }

    /// <summary>
    /// Number of retry attempts for Talabat sync
    /// </summary>
    public int TalabatSyncRetryCount { get; set; }

    /// <summary>
    /// Whether this deletion can be rolled back
    /// </summary>
    public bool CanRollback { get; set; } = true;

    /// <summary>
    /// When this deletion record expires and can be cleaned up
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual Foodics.FoodicsAccount FoodicsAccount { get; set; } = null!;
}

/// <summary>
/// Constants for deletion reasons
/// </summary>
public static class MenuDeletionReason
{
    public const string RemovedFromFoodics = "RemovedFromFoodics";
    public const string ManuallyDeleted = "ManuallyDeleted";
    public const string BranchUnavailable = "BranchUnavailable";
    public const string CategoryDeleted = "CategoryDeleted";
    public const string InactiveProduct = "InactiveProduct";
    public const string ComplianceIssue = "ComplianceIssue";
    public const string TemporaryUnavailable = "TemporaryUnavailable";
}

/// <summary>
/// Constants for deletion sources
/// </summary>
public static class MenuDeletionSource
{
    public const string FoodicsSync = "FoodicsSync";
    public const string ManualAdmin = "ManualAdmin";
    public const string BranchClosure = "BranchClosure";
    public const string AutoCleanup = "AutoCleanup";
    public const string ComplianceAction = "ComplianceAction";
    public const string SystemMaintenance = "SystemMaintenance";
}

/// <summary>
/// Constants for Talabat sync status
/// </summary>
public static class MenuDeletionSyncStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}