using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Stores historical versions of modifier options for rollback and audit purposes
/// Enables tracking of modifier option changes over time
/// </summary>
public class ModifierOptionVersion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to ModifierOption
    /// </summary>
    [Required]
    public Guid ModifierOptionId { get; set; }

    /// <summary>
    /// Version number of this snapshot
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Option name at this version
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
    /// Price at this version
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Price { get; set; }

    /// <summary>
    /// Active flag at this version
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Sort order at this version
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Image URL at this version
    /// </summary>
    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Property hash at this version
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string PropertyHash { get; set; } = string.Empty;

    /// <summary>
    /// When this version snapshot was created
    /// </summary>
    public DateTime SnapshotDate { get; set; }

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
    public virtual ModifierOption ModifierOption { get; set; } = null!;

    #region Business Methods

    /// <summary>
    /// Creates a version snapshot from current modifier option state
    /// </summary>
    public static ModifierOptionVersion CreateFromCurrent(ModifierOption modifierOption, string? changeReason = null, string? changedBy = null)
    {
        return new ModifierOptionVersion
        {
            ModifierOptionId = modifierOption.Id,
            Version = modifierOption.Version,
            Name = modifierOption.Name,
            NameLocalized = modifierOption.NameLocalized,
            Price = modifierOption.Price,
            IsActive = modifierOption.IsActive,
            SortOrder = modifierOption.SortOrder,
            ImageUrl = modifierOption.ImageUrl,
            PropertyHash = modifierOption.PropertyHash,
            SnapshotDate = DateTime.UtcNow,
            ChangeReason = changeReason,
            ChangedBy = changedBy,
            TenantId = modifierOption.TenantId
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

    /// <summary>
    /// Gets the price difference from this version to current
    /// </summary>
    public decimal GetPriceDifference(decimal currentPrice)
    {
        return currentPrice - Price;
    }

    #endregion
}