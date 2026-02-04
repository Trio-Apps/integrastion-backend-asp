using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace OrderXChange.Application.Versioning.DTOs;

/// <summary>
/// DTO for Menu Group information
/// </summary>
public class MenuGroupDto : FullAuditedEntityDto<Guid>
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID from Foodics (null for account-level Menu Groups)
    /// </summary>
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the Menu Group
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this Menu Group is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Sort order for Menu Group display
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// JSON metadata for flexible configuration
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Number of active categories in this Menu Group
    /// </summary>
    public int ActiveCategoriesCount { get; set; }

    /// <summary>
    /// Total number of products in this Menu Group (calculated)
    /// </summary>
    public int TotalProductsCount { get; set; }

    /// <summary>
    /// When this Menu Group was last synced
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Status of the last sync
    /// </summary>
    public string? LastSyncStatus { get; set; }

    /// <summary>
    /// Categories assigned to this Menu Group
    /// </summary>
    public List<MenuGroupCategoryDto> Categories { get; set; } = new();
}

/// <summary>
/// DTO for creating a new Menu Group
/// </summary>
public class CreateMenuGroupDto
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID from Foodics (null for account-level Menu Groups)
    /// </summary>
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group name (required)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the Menu Group
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for Menu Group display
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// JSON metadata for flexible configuration
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Initial categories to assign to this Menu Group
    /// </summary>
    public List<string> CategoryIds { get; set; } = new();
}

/// <summary>
/// DTO for updating an existing Menu Group
/// </summary>
public class UpdateMenuGroupDto
{
    /// <summary>
    /// Menu Group name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the Menu Group
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for Menu Group display
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// JSON metadata for flexible configuration
    /// </summary>
    public string? MetadataJson { get; set; }
}

/// <summary>
/// DTO for Menu Group category assignment
/// </summary>
public class MenuGroupCategoryDto : FullAuditedEntityDto<Guid>
{
    /// <summary>
    /// Foreign key to FoodicsMenuGroup
    /// </summary>
    public Guid MenuGroupId { get; set; }

    /// <summary>
    /// Category ID from Foodics
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this category assignment is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Sort order within the Menu Group
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// When this category was assigned to the Menu Group
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Category name (from Foodics data, for display purposes)
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// Number of products in this category (calculated)
    /// </summary>
    public int ProductsCount { get; set; }
}

/// <summary>
/// DTO for assigning a category to a Menu Group
/// </summary>
public class AssignCategoryDto
{
    /// <summary>
    /// Category ID from Foodics
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Sort order within the Menu Group
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// DTO for updating category sort order
/// </summary>
public class CategorySortOrderDto
{
    /// <summary>
    /// Category ID from Foodics
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// New sort order
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// DTO for Menu Group validation result
/// </summary>
public class MenuGroupValidationResultDto
{
    /// <summary>
    /// Whether the Menu Group is valid for sync
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Additional validation details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// DTO for Menu Group statistics
/// </summary>
public class MenuGroupStatisticsDto
{
    /// <summary>
    /// Menu Group ID
    /// </summary>
    public Guid MenuGroupId { get; set; }

    /// <summary>
    /// Total number of categories assigned
    /// </summary>
    public int TotalCategories { get; set; }

    /// <summary>
    /// Number of active categories
    /// </summary>
    public int ActiveCategories { get; set; }

    /// <summary>
    /// Total number of products across all categories
    /// </summary>
    public int TotalProducts { get; set; }

    /// <summary>
    /// Number of active products
    /// </summary>
    public int ActiveProducts { get; set; }

    /// <summary>
    /// Number of successful syncs
    /// </summary>
    public int SuccessfulSyncs { get; set; }

    /// <summary>
    /// Number of failed syncs
    /// </summary>
    public int FailedSyncs { get; set; }

    /// <summary>
    /// Last sync date
    /// </summary>
    public DateTime? LastSyncDate { get; set; }

    /// <summary>
    /// Average sync duration
    /// </summary>
    public TimeSpan? AverageSyncDuration { get; set; }

    /// <summary>
    /// Statistics by category
    /// </summary>
    public List<CategoryStatisticsDto> CategoryStatistics { get; set; } = new();
}

/// <summary>
/// DTO for category-level statistics within a Menu Group
/// </summary>
public class CategoryStatisticsDto
{
    /// <summary>
    /// Category ID from Foodics
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Category name
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// Number of products in this category
    /// </summary>
    public int ProductsCount { get; set; }

    /// <summary>
    /// Number of active products in this category
    /// </summary>
    public int ActiveProductsCount { get; set; }

    /// <summary>
    /// When this category was last updated
    /// </summary>
    public DateTime? LastUpdated { get; set; }
}