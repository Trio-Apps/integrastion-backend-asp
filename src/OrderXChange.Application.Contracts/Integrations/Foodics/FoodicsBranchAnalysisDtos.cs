using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrderXChange.Application.Integrations.Foodics;

/// <summary>
/// Enhanced aggregated menu DTO that includes branch-level analysis
/// Shows detailed breakdown of categories, menu groups, and products per branch
/// </summary>
public class FoodicsEnhancedAggregatedMenuDto
{
    /// <summary>
    /// Overall account summary
    /// </summary>
    public FoodicsAccountSummaryDto AccountSummary { get; set; } = new();

    /// <summary>
    /// Detailed analysis per branch
    /// </summary>
    public List<FoodicsBranchAnalysisDto> BranchAnalysis { get; set; } = new();

    /// <summary>
    /// Legacy categories structure (for backward compatibility)
    /// </summary>
    public List<FoodicsAggregatedCategoryDto> Categories { get; set; } = new();

    /// <summary>
    /// Legacy custom groups structure (for backward compatibility)
    /// </summary>
    public List<FoodicsAggregatedCustomGroupDto> Custom { get; set; } = new();
}

/// <summary>
/// Account-level summary statistics
/// </summary>
public class FoodicsAccountSummaryDto
{
    public int TotalBranches { get; set; }
    public int TotalProducts { get; set; }
    public int TotalCategories { get; set; }
    public int TotalMenuGroups { get; set; }
    public int ActiveProducts { get; set; }
    public int InactiveProducts { get; set; }
    public List<string> AllCategoryIds { get; set; } = new();
    public List<string> AllMenuGroupIds { get; set; } = new();
}

/// <summary>
/// Detailed analysis for a specific branch
/// </summary>
public class FoodicsBranchAnalysisDto
{
    /// <summary>
    /// Branch information
    /// </summary>
    public FoodicsBranchDto Branch { get; set; } = new();

    /// <summary>
    /// Branch-level statistics
    /// </summary>
    public FoodicsBranchStatsDto Stats { get; set; } = new();

    /// <summary>
    /// Categories available in this branch
    /// </summary>
    public List<FoodicsBranchCategoryDto> Categories { get; set; } = new();

    /// <summary>
    /// Menu groups available in this branch
    /// </summary>
    public List<FoodicsBranchMenuGroupDto> MenuGroups { get; set; } = new();

    /// <summary>
    /// All products available in this branch (organized by category)
    /// </summary>
    public List<FoodicsAggregatedCategoryDto> ProductsByCategory { get; set; } = new();
}

/// <summary>
/// Branch-level statistics
/// </summary>
public class FoodicsBranchStatsDto
{
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int InactiveProducts { get; set; }
    public int CategoriesCount { get; set; }
    public int MenuGroupsCount { get; set; }
    public int ProductsWithModifiers { get; set; }
    public int ProductsWithoutCategory { get; set; }
    public int ProductsWithoutMenuGroup { get; set; }
}

/// <summary>
/// Category information for a specific branch
/// </summary>
public class FoodicsBranchCategoryDto
{
    public FoodicsCategoryInfoDto Category { get; set; } = new();
    public int ProductCount { get; set; }
    public int ActiveProductCount { get; set; }
    public List<string> ProductIds { get; set; } = new();
}

/// <summary>
/// Menu group information for a specific branch
/// </summary>
public class FoodicsBranchMenuGroupDto
{
    public string GroupId { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public int ProductCount { get; set; }
    public int ActiveProductCount { get; set; }
    public List<string> ProductIds { get; set; } = new();
    public List<string> CategoryIds { get; set; } = new();
}

/// <summary>
/// Request DTO for enhanced aggregated menu
/// </summary>
public class GetEnhancedAggregatedMenuRequest
{
    /// <summary>
    /// Optional branch ID to filter analysis to specific branch
    /// If null, analyzes all branches
    /// </summary>
    public string? BranchId { get; set; }

    /// <summary>
    /// Optional Foodics Account ID
    /// If null, uses current tenant's account
    /// </summary>
    public System.Guid? FoodicsAccountId { get; set; }

    /// <summary>
    /// Whether to include detailed product information in response
    /// Default: true
    /// </summary>
    public bool IncludeProductDetails { get; set; } = true;

    /// <summary>
    /// Whether to include inactive products in analysis
    /// Default: false
    /// </summary>
    public bool IncludeInactiveProducts { get; set; } = false;

    /// <summary>
    /// Whether to include products without categories
    /// Default: true
    /// </summary>
    public bool IncludeUncategorizedProducts { get; set; } = true;
}