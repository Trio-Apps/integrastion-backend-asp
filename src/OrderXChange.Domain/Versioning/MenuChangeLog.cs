using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Detailed change log for menu modifications between versions.
/// Tracks individual product/category/modifier changes for audit and debugging.
/// </summary>
public class MenuChangeLog : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to MenuSnapshot (the new version)
    /// </summary>
    [Required]
    public Guid MenuSnapshotId { get; set; }

    /// <summary>
    /// Previous version number (null if this is the first version)
    /// </summary>
    public int? PreviousVersion { get; set; }

    /// <summary>
    /// Current version number
    /// </summary>
    public int CurrentVersion { get; set; }

    /// <summary>
    /// Type of change: Added, Removed, Modified
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// Entity type: Product, Category, Modifier, ModifierOption
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
    /// Entity name for quick reference
    /// </summary>
    [MaxLength(500)]
    public string? EntityName { get; set; }

    /// <summary>
    /// JSON representation of the old value (for Modified changes)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? OldValueJson { get; set; }

    /// <summary>
    /// JSON representation of the new value
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? NewValueJson { get; set; }

    /// <summary>
    /// Specific fields that changed (comma-separated)
    /// Example: "price,name,is_active"
    /// </summary>
    [MaxLength(1000)]
    public string? ChangedFields { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual MenuSnapshot MenuSnapshot { get; set; } = null!;
}

/// <summary>
/// Constants for change types
/// </summary>
public static class MenuChangeType
{
    public const string Added = "Added";
    public const string Removed = "Removed";
    public const string Modified = "Modified";
    public const string SoftDeleted = "SoftDeleted";
    public const string Restored = "Restored";
}

/// <summary>
/// Constants for entity types
/// </summary>
public static class MenuEntityType
{
    public const string Product = "Product";
    public const string Category = "Category";
    public const string Modifier = "Modifier";
    public const string ModifierOption = "ModifierOption";
}
