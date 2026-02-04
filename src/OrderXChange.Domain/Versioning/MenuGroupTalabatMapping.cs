using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Maps Foodics Menu Groups to Talabat menus with configurable isolation
/// Ensures one-to-one mapping between Menu Groups and Talabat menu contexts
/// </summary>
public class MenuGroupTalabatMapping : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    [Required]
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Foreign key to FoodicsMenuGroup
    /// </summary>
    [Required]
    public Guid MenuGroupId { get; set; }

    /// <summary>
    /// Talabat vendor code this Menu Group maps to
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TalabatVendorCode { get; set; } = string.Empty;

    /// <summary>
    /// Talabat menu identifier (unique per vendor)
    /// Generated automatically or configured manually
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string TalabatMenuId { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this menu in Talabat
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TalabatMenuName { get; set; } = string.Empty;

    /// <summary>
    /// Menu description in Talabat
    /// </summary>
    [MaxLength(2000)]
    public string? TalabatMenuDescription { get; set; }

    /// <summary>
    /// Whether this mapping is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Mapping priority (for conflict resolution)
    /// Higher values take precedence
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Mapping strategy: Auto, Manual, Template
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MappingStrategy { get; set; } = "Auto";

    /// <summary>
    /// JSON configuration for mapping behavior
    /// Contains isolation rules, naming patterns, sync preferences
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// When this mapping was first established
    /// </summary>
    public DateTime MappingEstablishedAt { get; set; }

    /// <summary>
    /// When this mapping was last verified/used
    /// </summary>
    public DateTime LastVerifiedAt { get; set; }

    /// <summary>
    /// Number of successful syncs using this mapping
    /// </summary>
    public int SyncCount { get; set; }

    /// <summary>
    /// Whether this mapping has been validated with Talabat
    /// </summary>
    public bool IsTalabatValidated { get; set; }

    /// <summary>
    /// Talabat-assigned internal menu ID (after validation)
    /// </summary>
    [MaxLength(200)]
    public string? TalabatInternalMenuId { get; set; }

    /// <summary>
    /// Current sync status of this mapping
    /// </summary>
    [MaxLength(50)]
    public string SyncStatus { get; set; } = MenuMappingSyncStatus.Pending;

    /// <summary>
    /// Last sync error details (if any)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? LastSyncError { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual Foodics.FoodicsAccount FoodicsAccount { get; set; } = null!;
    public virtual FoodicsMenuGroup MenuGroup { get; set; } = null!;

    #region Business Methods

    /// <summary>
    /// Activates this mapping
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates this mapping
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a successful sync using this mapping
    /// </summary>
    public void RecordSuccessfulSync()
    {
        SyncCount++;
        SyncStatus = MenuMappingSyncStatus.Synced;
        LastSyncError = null;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a sync failure
    /// </summary>
    public void RecordSyncFailure(string error)
    {
        SyncStatus = MenuMappingSyncStatus.Failed;
        LastSyncError = error;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets Talabat validation status
    /// </summary>
    public void SetTalabatValidated(string? internalMenuId = null)
    {
        IsTalabatValidated = true;
        TalabatInternalMenuId = internalMenuId;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the mapping configuration
    /// </summary>
    public void UpdateConfiguration(string configurationJson)
    {
        ConfigurationJson = configurationJson;
        LastVerifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the unique context key for this mapping
    /// </summary>
    public string GetContextKey()
    {
        return $"{FoodicsAccountId}:{MenuGroupId}:{TalabatVendorCode}:{TalabatMenuId}";
    }

    /// <summary>
    /// Validates the mapping configuration
    /// </summary>
    public MenuMappingValidationResult ValidateMapping()
    {
        var result = new MenuMappingValidationResult { IsValid = true };

        if (!IsActive)
        {
            result.IsValid = false;
            result.Errors.Add("Mapping is not active");
        }

        if (string.IsNullOrWhiteSpace(TalabatVendorCode))
        {
            result.IsValid = false;
            result.Errors.Add("Talabat vendor code is required");
        }

        if (string.IsNullOrWhiteSpace(TalabatMenuId))
        {
            result.IsValid = false;
            result.Errors.Add("Talabat menu ID is required");
        }

        if (string.IsNullOrWhiteSpace(TalabatMenuName))
        {
            result.IsValid = false;
            result.Errors.Add("Talabat menu name is required");
        }

        return result;
    }

    #endregion
}

/// <summary>
/// Constants for mapping sync status
/// </summary>
public static class MenuMappingSyncStatus
{
    public const string Pending = "Pending";
    public const string Syncing = "Syncing";
    public const string Synced = "Synced";
    public const string Failed = "Failed";
    public const string Outdated = "Outdated";
}

/// <summary>
/// Validation result for menu mapping
/// </summary>
public class MenuMappingValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}