using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Tracks menu versions per branch to detect changes and avoid unnecessary full re-syncs.
/// Stores a snapshot hash of the menu structure to enable efficient change detection.
/// </summary>
public class MenuSnapshot : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    [Required]
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID from Foodics (null for all-branches snapshot)
    /// </summary>
    [MaxLength(100)]
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group ID for scoped snapshots (null for branch-level snapshots)
    /// Enables Menu Group-specific versioning and change detection
    /// </summary>
    public Guid? MenuGroupId { get; set; }

    /// <summary>
    /// Unique version identifier (incremental)
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// SHA256 hash of the menu structure (products + categories + modifiers)
    /// Used for efficient change detection
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string SnapshotHash { get; set; } = string.Empty;

    /// <summary>
    /// Total number of products in this snapshot
    /// </summary>
    public int ProductsCount { get; set; }

    /// <summary>
    /// Total number of categories in this snapshot
    /// </summary>
    public int CategoriesCount { get; set; }

    /// <summary>
    /// Total number of modifiers across all products
    /// </summary>
    public int ModifiersCount { get; set; }

    /// <summary>
    /// When this snapshot was created (menu fetched from Foodics)
    /// </summary>
    public DateTime SnapshotDate { get; set; }

    /// <summary>
    /// Whether this snapshot was successfully synced to Talabat
    /// </summary>
    public bool IsSyncedToTalabat { get; set; }

    /// <summary>
    /// Talabat ImportId if synced
    /// </summary>
    [MaxLength(200)]
    public string? TalabatImportId { get; set; }

    /// <summary>
    /// When this snapshot was synced to Talabat
    /// </summary>
    public DateTime? TalabatSyncedAt { get; set; }

    /// <summary>
    /// Talabat vendor code this snapshot was synced to
    /// </summary>
    [MaxLength(100)]
    public string? TalabatVendorCode { get; set; }

    /// <summary>
    /// JSON metadata about what changed compared to previous version
    /// Format: { "added": [...], "removed": [...], "modified": [...] }
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? ChangelogJson { get; set; }

    /// <summary>
    /// Compressed snapshot data (optional - for rollback capability)
    /// Stores full menu structure in compressed JSON format
    /// </summary>
    [Column(TypeName = "LONGBLOB")]
    public byte[]? CompressedSnapshotData { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual Foodics.FoodicsAccount FoodicsAccount { get; set; } = null!;
    public virtual FoodicsMenuGroup? MenuGroup { get; set; }
}
