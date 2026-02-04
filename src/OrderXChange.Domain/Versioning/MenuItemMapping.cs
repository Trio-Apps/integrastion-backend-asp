using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Permanent mapping between Foodics and Talabat menu item IDs
/// Ensures data integrity and prevents mapping breaks on renames
/// </summary>
public class MenuItemMapping : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    [Required]
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID (null for all branches)
    /// </summary>
    [MaxLength(100)]
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group ID for scoped mappings (null for branch-level mappings)
    /// Enables Menu Group-specific stable ID mapping and prevents conflicts
    /// </summary>
    public Guid? MenuGroupId { get; set; }

    /// <summary>
    /// Type of menu entity: Product, Category, Modifier, ModifierOption
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Foodics entity ID (primary reference - never changes)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FoodicsId { get; set; } = string.Empty;

    /// <summary>
    /// Talabat remote code (assigned by us, stable across syncs)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TalabatRemoteCode { get; set; } = string.Empty;

    /// <summary>
    /// Talabat internal ID (assigned by Talabat after successful import)
    /// </summary>
    [MaxLength(100)]
    public string? TalabatInternalId { get; set; }

    /// <summary>
    /// Current name in Foodics (for reference only, not used for mapping)
    /// </summary>
    [MaxLength(500)]
    public string? CurrentFoodicsName { get; set; }

    /// <summary>
    /// Current name in Talabat (for reference only, not used for mapping)
    /// </summary>
    [MaxLength(500)]
    public string? CurrentTalabatName { get; set; }

    /// <summary>
    /// Parent entity mapping ID (for hierarchical relationships)
    /// </summary>
    public Guid? ParentMappingId { get; set; }

    /// <summary>
    /// Whether this mapping is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this mapping was first created
    /// </summary>
    public DateTime FirstSyncedAt { get; set; }

    /// <summary>
    /// When this mapping was last verified/updated
    /// </summary>
    public DateTime LastVerifiedAt { get; set; }

    /// <summary>
    /// Number of successful syncs using this mapping
    /// </summary>
    public int SyncCount { get; set; }

    /// <summary>
    /// Hash of the entity structure for change detection
    /// </summary>
    [MaxLength(64)]
    public string? StructureHash { get; set; }

    /// <summary>
    /// JSON metadata about the mapping
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual Foodics.FoodicsAccount FoodicsAccount { get; set; } = null!;
    public virtual FoodicsMenuGroup? MenuGroup { get; set; }
    public virtual MenuItemMapping? ParentMapping { get; set; }
    public virtual ICollection<MenuItemMapping> ChildMappings { get; set; } = new List<MenuItemMapping>();

    #region Business Methods

    /// <summary>
    /// Updates the current names for reference (doesn't affect mapping)
    /// </summary>
    public void UpdateNames(string? foodicsName, string? talabatName = null)
    {
        CurrentFoodicsName = foodicsName;
        if (!string.IsNullOrEmpty(talabatName))
        {
            CurrentTalabatName = talabatName;
        }
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a successful sync using this mapping
    /// </summary>
    public void RecordSuccessfulSync()
    {
        SyncCount++;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the Talabat internal ID after successful import
    /// </summary>
    public void SetTalabatInternalId(string talabatInternalId)
    {
        TalabatInternalId = talabatInternalId;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the structure hash for change detection
    /// </summary>
    public void UpdateStructureHash(string structureHash)
    {
        StructureHash = structureHash;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates this mapping (soft delete)
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reactivates this mapping
    /// </summary>
    public void Reactivate()
    {
        IsActive = true;
        LastVerifiedAt = DateTime.UtcNow;
    }

    #endregion
}

/// <summary>
/// Constants for menu entity types in mapping
/// </summary>
public static class MenuMappingEntityType
{
    public const string Product = "Product";
    public const string Category = "Category";
    public const string Modifier = "Modifier";
    public const string ModifierOption = "ModifierOption";
}

/// <summary>
/// Mapping generation strategy with Menu Group support
/// </summary>
public static class MenuMappingStrategy
{
    /// <summary>
    /// Automatic mapping strategy - system generates mappings automatically
    /// </summary>
    public const string Auto = "Auto";

    /// <summary>
    /// Manual mapping strategy - user defines mappings explicitly
    /// </summary>
    public const string Manual = "Manual";

    /// <summary>
    /// Template-based mapping strategy - uses predefined templates
    /// </summary>
    public const string Template = "Template";

    /// <summary>
    /// Generates a stable Talabat remote code from Foodics ID with optional Menu Group context
    /// </summary>
    public static string GenerateTalabatRemoteCode(string entityType, string foodicsId, Guid? menuGroupId = null)
    {
        // Use a prefix to avoid conflicts and ensure uniqueness
        var prefix = entityType switch
        {
            MenuMappingEntityType.Product => "P",
            MenuMappingEntityType.Category => "C",
            MenuMappingEntityType.Modifier => "M",
            MenuMappingEntityType.ModifierOption => "O",
            _ => "X"
        };

        // Include Menu Group context in remote code for scoped mappings
        if (menuGroupId.HasValue)
        {
            // Use first 8 characters of Menu Group ID for uniqueness while keeping codes readable
            var groupSuffix = menuGroupId.Value.ToString("N")[..8].ToUpperInvariant();
            return $"{prefix}_{foodicsId}_{groupSuffix}";
        }

        // Use Foodics ID directly with prefix for maximum stability (backward compatibility)
        return $"{prefix}_{foodicsId}";
    }

    /// <summary>
    /// Extracts Foodics ID from Talabat remote code
    /// </summary>
    public static string? ExtractFoodicsId(string talabatRemoteCode)
    {
        if (string.IsNullOrEmpty(talabatRemoteCode))
            return null;

        var parts = talabatRemoteCode.Split('_');
        return parts.Length >= 2 ? parts[1] : null;
    }

    /// <summary>
    /// Extracts entity type from Talabat remote code
    /// </summary>
    public static string? ExtractEntityType(string talabatRemoteCode)
    {
        if (string.IsNullOrEmpty(talabatRemoteCode))
            return null;

        var parts = talabatRemoteCode.Split('_');
        if (parts.Length < 2)
            return null;

        return parts[0] switch
        {
            "P" => MenuMappingEntityType.Product,
            "C" => MenuMappingEntityType.Category,
            "M" => MenuMappingEntityType.Modifier,
            "O" => MenuMappingEntityType.ModifierOption,
            _ => null
        };
    }

    /// <summary>
    /// Extracts Menu Group suffix from Talabat remote code
    /// </summary>
    public static string? ExtractMenuGroupSuffix(string talabatRemoteCode)
    {
        if (string.IsNullOrEmpty(talabatRemoteCode))
            return null;

        var parts = talabatRemoteCode.Split('_');
        return parts.Length == 3 ? parts[2] : null;
    }

    /// <summary>
    /// Checks if a remote code is Menu Group-scoped
    /// </summary>
    public static bool IsMenuGroupScoped(string talabatRemoteCode)
    {
        if (string.IsNullOrEmpty(talabatRemoteCode))
            return false;

        var parts = talabatRemoteCode.Split('_');
        return parts.Length == 3;
    }
}