using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Represents a modifier group with versioning and lifecycle tracking
/// Tracks modifier groups across menu versions and handles price changes safely
/// </summary>
public class ModifierGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    [Required]
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID from Foodics (null for account-level modifiers)
    /// </summary>
    [MaxLength(100)]
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group ID for scoped modifiers (null for branch-level modifiers)
    /// </summary>
    public Guid? MenuGroupId { get; set; }

    /// <summary>
    /// Foodics modifier group ID (remote identifier)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FoodicsModifierGroupId { get; set; } = string.Empty;

    /// <summary>
    /// Modifier group name
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Localized name (if available)
    /// </summary>
    [MaxLength(500)]
    public string? NameLocalized { get; set; }

    /// <summary>
    /// Current version of this modifier group
    /// Incremented when structure or options change
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Minimum number of options that must be selected
    /// </summary>
    public int? MinSelection { get; set; }

    /// <summary>
    /// Maximum number of options that can be selected
    /// </summary>
    public int? MaxSelection { get; set; }

    /// <summary>
    /// Whether this modifier group is required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Whether this modifier group is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order for this modifier group
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Hash of the modifier group structure (options + prices)
    /// Used for change detection
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string StructureHash { get; set; } = string.Empty;

    /// <summary>
    /// When this modifier group was last updated in Foodics
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// Whether this modifier group has been synced to Talabat
    /// </summary>
    public bool IsSyncedToTalabat { get; set; }

    /// <summary>
    /// When this modifier group was last synced to Talabat
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Talabat vendor code this modifier was synced to
    /// </summary>
    [MaxLength(100)]
    public string? TalabatVendorCode { get; set; }

    /// <summary>
    /// JSON metadata for additional properties
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
    public virtual ICollection<ModifierOption> Options { get; set; } = new List<ModifierOption>();
    public virtual ICollection<ModifierGroupVersion> Versions { get; set; } = new List<ModifierGroupVersion>();
    public virtual ICollection<ProductModifierAssignment> ProductAssignments { get; set; } = new List<ProductModifierAssignment>();

    #region Business Methods

    /// <summary>
    /// Updates the modifier group structure and increments version
    /// </summary>
    public void UpdateStructure(string name, string? nameLocalized, int? minSelection, int? maxSelection, bool isRequired, string structureHash)
    {
        var hasChanges = Name != name || 
                        NameLocalized != nameLocalized || 
                        MinSelection != minSelection || 
                        MaxSelection != maxSelection || 
                        IsRequired != isRequired ||
                        StructureHash != structureHash;

        if (hasChanges)
        {
            // Create version snapshot before updating
            CreateVersionSnapshot();

            Name = name;
            NameLocalized = nameLocalized;
            MinSelection = minSelection;
            MaxSelection = maxSelection;
            IsRequired = isRequired;
            StructureHash = structureHash;
            Version++;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false; // Mark as needing sync
        }
    }

    /// <summary>
    /// Activates the modifier group
    /// </summary>
    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false;
        }
    }

    /// <summary>
    /// Deactivates the modifier group
    /// </summary>
    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false;
        }
    }

    /// <summary>
    /// Marks as synced to Talabat
    /// </summary>
    public void MarkAsSynced(string talabatVendorCode)
    {
        IsSyncedToTalabat = true;
        LastSyncedAt = DateTime.UtcNow;
        TalabatVendorCode = talabatVendorCode;
    }

    /// <summary>
    /// Gets the unique context key for this modifier group
    /// </summary>
    public string GetContextKey()
    {
        return $"{FoodicsAccountId}:{BranchId ?? "ALL"}:{MenuGroupId?.ToString() ?? "ALL"}:{FoodicsModifierGroupId}";
    }

    /// <summary>
    /// Validates the modifier group configuration
    /// </summary>
    public ModifierGroupValidationResult ValidateConfiguration()
    {
        var result = new ModifierGroupValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(Name))
        {
            result.IsValid = false;
            result.Errors.Add("Modifier group name is required");
        }

        if (MinSelection.HasValue && MinSelection.Value < 0)
        {
            result.IsValid = false;
            result.Errors.Add("MinSelection cannot be negative");
        }

        if (MaxSelection.HasValue && MaxSelection.Value <= 0)
        {
            result.IsValid = false;
            result.Errors.Add("MaxSelection must be greater than 0");
        }

        if (MinSelection.HasValue && MaxSelection.HasValue && MinSelection.Value > MaxSelection.Value)
        {
            result.IsValid = false;
            result.Errors.Add("MinSelection cannot be greater than MaxSelection");
        }

        var activeOptions = Options.Where(o => o.IsActive).ToList();
        if (!activeOptions.Any())
        {
            result.Warnings.Add("Modifier group has no active options");
        }

        if (IsRequired && MinSelection.HasValue && MinSelection.Value == 0)
        {
            result.Warnings.Add("Required modifier group has MinSelection of 0");
        }

        return result;
    }

    /// <summary>
    /// Creates a version snapshot before making changes
    /// </summary>
    private void CreateVersionSnapshot()
    {
        var versionSnapshot = new ModifierGroupVersion
        {
            ModifierGroupId = Id,
            Version = Version,
            Name = Name,
            NameLocalized = NameLocalized,
            MinSelection = MinSelection,
            MaxSelection = MaxSelection,
            IsRequired = IsRequired,
            IsActive = IsActive,
            StructureHash = StructureHash,
            SnapshotDate = DateTime.UtcNow,
            TenantId = TenantId
        };

        Versions.Add(versionSnapshot);
    }

    #endregion
}

/// <summary>
/// Validation result for modifier group configuration
/// </summary>
public class ModifierGroupValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}