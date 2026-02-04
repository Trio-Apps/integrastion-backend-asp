using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsListEnvelope<TItem>
{
	[JsonPropertyName("data")]
	public List<TItem> Data { get; set; } = [];
}

public class FoodicsProductAvailabilityListEnvelope
{
	[JsonPropertyName("data")]
	public List<FoodicsProductDetailDto>? Data { get; set; }

	[JsonPropertyName("meta")]
	public FoodicsMetaDto? Meta { get; set; }
}

public class FoodicsMetaDto
{
	[JsonPropertyName("total")]
	public int? Total { get; set; }

	[JsonPropertyName("per_page")]
	public int? PerPage { get; set; }

	[JsonPropertyName("current_page")]
	public int? CurrentPage { get; set; }
}

public class FoodicsCategoryInfoDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }
}

public class FoodicsProductInfoDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }

	[JsonPropertyName("price")]
	public decimal? Price { get; set; }
}

public class FoodicsAggregatedMenuDto
{
	public List<FoodicsAggregatedCategoryDto> Categories { get; set; } = [];
	public List<FoodicsAggregatedCustomGroupDto> Custom { get; set; } = [];
}

public class FoodicsAggregatedCategoryDto
{
	public FoodicsCategoryInfoDto Category { get; set; } = new();
	public List<FoodicsAggregatedChildDto> Children { get; set; } = [];
}

public class FoodicsAggregatedCustomGroupDto
{
	public string GroupId { get; set; } = string.Empty;
	public List<FoodicsAggregatedChildDto> Children { get; set; } = [];
}

public class FoodicsAggregatedChildDto
{
	public string Type { get; set; } = string.Empty;
	public string Id { get; set; } = string.Empty;
	
	/// <summary>
	/// Full product details including all includes: price_tags, tax_group, tags, branches, modifiers, groups, etc.
	/// </summary>
	public FoodicsProductDetailDto? Product { get; set; }
}

public class FoodicsProductDetailDto : FoodicsProductInfoDto
{
	[JsonPropertyName("description")]
	public string? Description { get; set; }

	[JsonPropertyName("description_localized")]
	public string? DescriptionLocalized { get; set; }

	[JsonPropertyName("image")]
	public string? Image { get; set; }

	[JsonPropertyName("is_active")]
	public bool? IsActive { get; set; }

	[JsonPropertyName("sku")]
	public string? Sku { get; set; }

	[JsonPropertyName("barcode")]
	public string? Barcode { get; set; }

	[JsonPropertyName("category_id")]
	public string? CategoryId { get; set; }

	[JsonPropertyName("tax_group_id")]
	public string? TaxGroupId { get; set; }

	[JsonPropertyName("category")]
	public FoodicsCategoryInfoDto? Category { get; set; }

	[JsonPropertyName("price_tags")]
	public List<FoodicsPriceTagDto>? PriceTags { get; set; }

	[JsonPropertyName("tax_group")]
	public FoodicsTaxGroupDto? TaxGroup { get; set; }

	[JsonPropertyName("tags")]
	public List<FoodicsTagDto>? Tags { get; set; }

	[JsonPropertyName("branches")]
	public List<FoodicsBranchDto>? Branches { get; set; }

	[JsonPropertyName("ingredients")]
	public List<FoodicsIngredientDto>? Ingredients { get; set; }

	[JsonPropertyName("modifiers")]
	public List<FoodicsModifierDto>? Modifiers { get; set; }

	[JsonPropertyName("discounts")]
	public List<FoodicsDiscountDto>? Discounts { get; set; }

	[JsonPropertyName("timed_events")]
	public List<FoodicsTimedEventDto>? TimedEvents { get; set; }

	[JsonPropertyName("groups")]
	public List<FoodicsGroupInfoDto>? Groups { get; set; }

	[JsonPropertyName("deleted_at")]
	public string? DeletedAt { get; set; }
}

public class FoodicsPriceTagDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }

	[JsonPropertyName("price")]
	public decimal? Price { get; set; }
}

public class FoodicsTaxGroupDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }

	[JsonPropertyName("rate")]
	public decimal? Rate { get; set; }
}

public class FoodicsTagDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }
}

public class FoodicsIngredientDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("branches")]
	public List<FoodicsBranchDto>? Branches { get; set; }
}

public class FoodicsModifierDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }

	[JsonPropertyName("min_allowed")]
	public int? MinAllowed { get; set; }

	[JsonPropertyName("max_allowed")]
	public int? MaxAllowed { get; set; }

	[JsonPropertyName("options")]
	public List<FoodicsModifierOptionDto>? Options { get; set; }
}

public class FoodicsModifierOptionDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }

	[JsonPropertyName("price")]
	public decimal? Price { get; set; }

	[JsonPropertyName("image")]
	public string? Image { get; set; }

	[JsonPropertyName("branches")]
	public List<FoodicsBranchDto>? Branches { get; set; }
}

public class FoodicsDiscountDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("discount_type")]
	public string? DiscountType { get; set; }

	[JsonPropertyName("discount_value")]
	public decimal? DiscountValue { get; set; }
}

public class FoodicsTimedEventDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("start_time")]
	public string? StartTime { get; set; }

	[JsonPropertyName("end_time")]
	public string? EndTime { get; set; }
}

public class FoodicsGroupInfoDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }
}

/// <summary>
/// Extended group info with product count for dropdown selection.
/// Used when configuring TalabatAccount group filtering.
/// </summary>
public class FoodicsGroupWithProductCountDto
{
	public string Id { get; set; } = string.Empty;
	public string? Name { get; set; }
	public string? NameLocalized { get; set; }
	public int ProductCount { get; set; }
}
