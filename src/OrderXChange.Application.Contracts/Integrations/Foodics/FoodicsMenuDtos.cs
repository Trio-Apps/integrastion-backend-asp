using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsMenuResponseDto
{
    public FoodicsMenuDataDto Data { get; set; } = new();
}

public class FoodicsMenuDataDto
{
    public List<FoodicsCategoryDto> Categories { get; set; } = [];
    public List<FoodicsProductDto> Products { get; set; } = [];
    public List<FoodicsComboDto> Combos { get; set; } = [];
    public List<FoodicsBranchDto> Branches { get; set; } = [];
}

public class FoodicsCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public int? DisplayOrder { get; set; }
    public List<FoodicsRelationDto> Products { get; set; } = [];
    public List<FoodicsRelationDto> Combos { get; set; } = [];
}

public class FoodicsProductDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public string? Description { get; set; }
    public string? DescriptionLocalized { get; set; }
    public decimal? Price { get; set; }
    public decimal? Cost { get; set; }
    public bool? IsActive { get; set; }
    public FoodicsProductAvailabilityDto? Availability { get; set; }
    public List<FoodicsOptionGroupDto> OptionGroups { get; set; } = [];
}

public class FoodicsProductAvailabilityDto
{
    public List<FoodicsAvailabilityWindowDto> Windows { get; set; } = [];
}

public class FoodicsAvailabilityWindowDto
{
    public int? DayOfWeek { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

public class FoodicsOptionGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public int? MinOptions { get; set; }
    public int? MaxOptions { get; set; }
    public List<FoodicsOptionItemDto> Options { get; set; } = [];
}

public class FoodicsOptionItemDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public decimal? Price { get; set; }
    public bool? IsDefault { get; set; }
}

public class FoodicsComboDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public decimal? Price { get; set; }
    public List<FoodicsComboGroupDto> Groups { get; set; } = [];
}

public class FoodicsComboGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public int? MinProducts { get; set; }
    public int? MaxProducts { get; set; }
    public List<FoodicsRelationDto> Products { get; set; } = [];
}

public class FoodicsBranchDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("name_localized")]
    public string? NameLocalized { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    [JsonPropertyName("is_open")]
    public bool? IsOpen { get; set; }

    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }

    /// <summary>
    /// Pivot data containing branch-specific product information:
    /// - price: Branch-specific price override
    /// - is_active: Whether the product is active in this branch
    /// - is_in_stock: Stock availability status
    /// </summary>
    [JsonPropertyName("pivot")]
    public FoodicsBranchPivotDto? Pivot { get; set; }
}

/// <summary>
/// Branch-specific product data from the pivot relationship in Foodics API.
/// Contains pricing and availability information specific to each branch.
/// </summary>
public class FoodicsBranchPivotDto
{
    /// <summary>
    /// Branch-specific price override (in fils/cents)
    /// </summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    /// <summary>
    /// Whether the product is active/available in this specific branch
    /// </summary>
    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }

    /// <summary>
    /// Whether the product is in stock in this branch
    /// </summary>
    [JsonPropertyName("is_in_stock")]
    public bool? IsInStock { get; set; }
}

public class FoodicsRelationDto
{
    public string Id { get; set; } = string.Empty;
}

