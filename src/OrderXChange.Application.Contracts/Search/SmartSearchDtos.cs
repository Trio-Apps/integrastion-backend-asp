using System;
using System.Collections.Generic;

namespace OrderXChange.Search;

/// <summary>
/// Input for the unified "Smart Search" — one keyword / partial-text query fanned out across
/// orders, catalog items &amp; modifiers, and branches (BRS §4).
/// </summary>
public class SmartSearchInput
{
    /// <summary>The keyword / partial text to look for. Blank input yields an empty result.</summary>
    public string? Keyword { get; set; }

    /// <summary>Maximum results returned per group. Clamped to 1..50 (default 10).</summary>
    public int MaxResultsPerGroup { get; set; } = 10;
}

/// <summary>Grouped Smart Search results. Each group is permission-trimmed and branch-scoped.</summary>
public class SmartSearchResultDto
{
    /// <summary>The (trimmed) keyword the results were produced for.</summary>
    public string Keyword { get; set; } = string.Empty;

    public List<SmartSearchOrderDto> Orders { get; set; } = [];

    public List<SmartSearchItemDto> Items { get; set; } = [];

    public List<SmartSearchBranchDto> Branches { get; set; } = [];

    /// <summary>Total number of results across all groups.</summary>
    public int TotalCount { get; set; }
}

public class SmartSearchOrderDto
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public string? ShortCode { get; set; }
    public string? OrderToken { get; set; }
    public string? VendorCode { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Status { get; set; }
    public DateTime? ReceivedAt { get; set; }
}

public class SmartSearchItemDto
{
    public Guid Id { get; set; }
    public string? FoodicsProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameLocalized { get; set; }
    public string? Sku { get; set; }
    public string? CategoryName { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Modifier option names (parsed from the product's modifiers) that matched the keyword.</summary>
    public List<string> MatchedModifiers { get; set; } = [];
}

public class SmartSearchBranchDto
{
    public string BranchId { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
}
