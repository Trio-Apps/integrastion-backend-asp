using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace OrderXChange.Application.Versioning.DTOs;

/// <summary>
/// DTO for Menu Group to Talabat mapping operations
/// </summary>
public class MenuGroupTalabatMappingDto : FullAuditedEntityDto<Guid>
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Foreign key to FoodicsMenuGroup
    /// </summary>
    public Guid MenuGroupId { get; set; }

    /// <summary>
    /// Menu Group name (for display purposes)
    /// </summary>
    public string MenuGroupName { get; set; } = string.Empty;

    /// <summary>
    /// Talabat vendor code this Menu Group maps to
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TalabatVendorCode { get; set; } = string.Empty;

    /// <summary>
    /// Talabat menu identifier (unique per vendor)
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
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Mapping strategy: Auto, Manual, Template
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MappingStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Configuration for mapping behavior
    /// </summary>
    public MenuGroupMappingConfigurationDto? Configuration { get; set; }

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
    public string SyncStatus { get; set; } = string.Empty;

    /// <summary>
    /// Last sync error details (if any)
    /// </summary>
    public string? LastSyncError { get; set; }
}

/// <summary>
/// DTO for creating Menu Group to Talabat mappings
/// </summary>
public class CreateMenuGroupTalabatMappingDto
{
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
    /// Generated automatically if not provided
    /// </summary>
    [MaxLength(200)]
    public string? TalabatMenuId { get; set; }

    /// <summary>
    /// Display name for this menu in Talabat
    /// Uses Menu Group name if not provided
    /// </summary>
    [MaxLength(500)]
    public string? TalabatMenuName { get; set; }

    /// <summary>
    /// Menu description in Talabat
    /// </summary>
    [MaxLength(2000)]
    public string? TalabatMenuDescription { get; set; }

    /// <summary>
    /// Mapping priority (for conflict resolution)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Mapping strategy: Auto, Manual, Template
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MappingStrategy { get; set; } = "Auto";

    /// <summary>
    /// Configuration for mapping behavior
    /// </summary>
    public MenuGroupMappingConfigurationDto? Configuration { get; set; }
}

/// <summary>
/// DTO for updating Menu Group to Talabat mappings
/// </summary>
public class UpdateMenuGroupTalabatMappingDto
{
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
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Mapping strategy: Auto, Manual, Template
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MappingStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Configuration for mapping behavior
    /// </summary>
    public MenuGroupMappingConfigurationDto? Configuration { get; set; }
}

/// <summary>
/// Configuration DTO for Menu Group mapping behavior
/// </summary>
public class MenuGroupMappingConfigurationDto
{
    /// <summary>
    /// Whether to isolate this menu from other Menu Groups
    /// </summary>
    public bool IsolateMenu { get; set; } = true;

    /// <summary>
    /// Naming pattern for generated Talabat menu items
    /// Supports placeholders: {menuGroupName}, {categoryName}, {itemName}
    /// </summary>
    public string? NamingPattern { get; set; }

    /// <summary>
    /// Whether to include Menu Group name in item names
    /// </summary>
    public bool IncludeMenuGroupInNames { get; set; } = false;

    /// <summary>
    /// Whether to sync availability status
    /// </summary>
    public bool SyncAvailability { get; set; } = true;

    /// <summary>
    /// Whether to sync pricing information
    /// </summary>
    public bool SyncPricing { get; set; } = true;

    /// <summary>
    /// Whether to sync modifiers
    /// </summary>
    public bool SyncModifiers { get; set; } = true;

    /// <summary>
    /// Whether to sync images
    /// </summary>
    public bool SyncImages { get; set; } = true;

    /// <summary>
    /// Custom field mappings (JSON key-value pairs)
    /// </summary>
    public Dictionary<string, string>? CustomFieldMappings { get; set; }

    /// <summary>
    /// Validation rules for this mapping
    /// </summary>
    public MenuGroupValidationRulesDto? ValidationRules { get; set; }

    /// <summary>
    /// Sync preferences
    /// </summary>
    public MenuGroupSyncPreferencesDto? SyncPreferences { get; set; }
}

/// <summary>
/// Validation rules for Menu Group mapping
/// </summary>
public class MenuGroupValidationRulesDto
{
    /// <summary>
    /// Minimum number of items required
    /// </summary>
    public int? MinItemCount { get; set; }

    /// <summary>
    /// Maximum number of items allowed
    /// </summary>
    public int? MaxItemCount { get; set; }

    /// <summary>
    /// Required categories (category IDs)
    /// </summary>
    public List<string>? RequiredCategories { get; set; }

    /// <summary>
    /// Excluded categories (category IDs)
    /// </summary>
    public List<string>? ExcludedCategories { get; set; }

    /// <summary>
    /// Whether all items must have prices
    /// </summary>
    public bool RequirePrices { get; set; } = true;

    /// <summary>
    /// Whether all items must have descriptions
    /// </summary>
    public bool RequireDescriptions { get; set; } = false;

    /// <summary>
    /// Whether all items must have images
    /// </summary>
    public bool RequireImages { get; set; } = false;
}

/// <summary>
/// Sync preferences for Menu Group mapping
/// </summary>
public class MenuGroupSyncPreferencesDto
{
    /// <summary>
    /// Whether to auto-sync when Menu Group changes
    /// </summary>
    public bool AutoSync { get; set; } = true;

    /// <summary>
    /// Sync frequency in minutes (for scheduled syncs)
    /// </summary>
    public int? SyncFrequencyMinutes { get; set; }

    /// <summary>
    /// Whether to sync during off-peak hours only
    /// </summary>
    public bool OffPeakOnly { get; set; } = false;

    /// <summary>
    /// Time zone for sync scheduling
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// Preferred sync hours (24-hour format)
    /// </summary>
    public List<int>? PreferredSyncHours { get; set; }

    /// <summary>
    /// Whether to batch sync with other Menu Groups
    /// </summary>
    public bool BatchSync { get; set; } = false;

    /// <summary>
    /// Maximum retry attempts for failed syncs
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in minutes
    /// </summary>
    public int RetryDelayMinutes { get; set; } = 5;
}