using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Stores historical versions of modifier groups for rollback and audit purposes
/// Enables tracking of modifier group structure changes over time
/// </summary>
public class ModifierGroupVersion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to ModifierGroup
    /// </summary>
    [Required]
    public Guid ModifierGroupId { get; set; }

    /// <summary>
    /// Version number of this snapshot
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Modifier group name at this version
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Localized name at this version
    /// </summary>
    [MaxLength(500)]
    public string? NameLocalized { get; set; }

    /// <summary>
    /// Minimum selection at this version
    /// </summary>
    public int? MinSelection { get; set; }

    /// <summary>
    /// Maximum selection at this version
    /// </summary>
    public int? MaxSelection { get; set; }

    /// <summary>
    /// Required flag at this version
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Active flag at this version
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Structure hash at this version
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string StructureHash { get; set; } = string.Empty;

    /// <summary>
    /// When this version snapshot was created
    /// </summary>
    public DateTime SnapshotDate { get; set; }

    /// <summary>
    /// Compressed snapshot of options at this version (JSON)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? OptionsSnapshot { get; set; }

    /// <summary>
    /// Reason for this version change
    /// </summary>
    [MaxLength(1000)]
    public string? ChangeReason { get; set; }

    /// <summary>
    /// Who made the change that created this version
    /// </summary>
    [MaxLength(200)]
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual ModifierGroup ModifierGroup { get; set; } = null!;

    #region Business Methods

    /// <summary>
    /// Creates a version snapshot from current modifier group state
    /// </summary>
    public static ModifierGroupVersion CreateFromCurrent(ModifierGroup modifierGroup, string? changeReason = null, string? changedBy = null)
    {
        return new ModifierGroupVersion
        {
            ModifierGroupId = modifierGroup.Id,
            Version = modifierGroup.Version,
            Name = modifierGroup.Name,
            NameLocalized = modifierGroup.NameLocalized,
            MinSelection = modifierGroup.MinSelection,
            MaxSelection = modifierGroup.MaxSelection,
            IsRequired = modifierGroup.IsRequired,
            IsActive = modifierGroup.IsActive,
            StructureHash = modifierGroup.StructureHash,
            SnapshotDate = DateTime.UtcNow,
            ChangeReason = changeReason,
            ChangedBy = changedBy,
            TenantId = modifierGroup.TenantId
        };
    }

    /// <summary>
    /// Checks if this version can be used for rollback
    /// </summary>
    public bool CanRollback()
    {
        // Don't allow rollback to very old versions (older than 30 days)
        return SnapshotDate >= DateTime.UtcNow.AddDays(-30);
    }

    #endregion
}