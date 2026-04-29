using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsListEnvelope<TItem>
{
	[JsonPropertyName("data")]
	public List<TItem> Data { get; set; } = [];

	[JsonPropertyName("meta")]
	public FoodicsMetaDto? Meta { get; set; }
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

	[JsonPropertyName("pivot")]
	public FoodicsProductGroupPivotDto? Pivot { get; set; }

	[JsonPropertyName("deleted_at")]
	public string? DeletedAt { get; set; }
}

public class FoodicsProductGroupPivotDto
{
	[JsonPropertyName("is_active")]
	public bool? IsActive { get; set; }
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
	private int? _minAllowed;
	private int? _maxAllowed;

	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("name_localized")]
	public string? NameLocalized { get; set; }

	[JsonPropertyName("min_allowed")]
	public int? MinAllowed
	{
		get => _minAllowed ?? Pivot?.MinimumOptions;
		set => _minAllowed = value;
	}

	[JsonPropertyName("max_allowed")]
	public int? MaxAllowed
	{
		get => _maxAllowed ?? Pivot?.MaximumOptions;
		set => _maxAllowed = value;
	}

	[JsonPropertyName("pivot")]
	public FoodicsModifierPivotDto? Pivot { get; set; }

	[JsonIgnore]
	public int? RawMinAllowed => _minAllowed;

	[JsonIgnore]
	public int? RawMaxAllowed => _maxAllowed;

	[JsonPropertyName("options")]
	public List<FoodicsModifierOptionDto>? Options { get; set; }
}

public class FoodicsModifierPivotDto
{
	[JsonPropertyName("index")]
	public int? Index { get; set; }

	[JsonPropertyName("minimum_options")]
	public int? MinimumOptions { get; set; }

	[JsonPropertyName("maximum_options")]
	public int? MaximumOptions { get; set; }

	[JsonPropertyName("free_options")]
	public int? FreeOptions { get; set; }

	[JsonPropertyName("unique_options")]
	public bool? UniqueOptions { get; set; }

	[JsonPropertyName("default_options_ids")]
	public List<string>? DefaultOptionIds { get; set; }

	[JsonPropertyName("excluded_options_ids")]
	public List<string>? ExcludedOptionIds { get; set; }
}

public class FoodicsModifierOptionDto
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("index")]
	public int? Index { get; set; }

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

	[JsonPropertyName("products")]
	public List<FoodicsProductDetailDto>? Products { get; set; }

	[JsonPropertyName("items_index")]
	[JsonConverter(typeof(FlexibleStringListJsonConverter))]
	public List<string>? ItemsIndex { get; set; }

	[JsonPropertyName("pivot")]
	public FoodicsGroupPivotDto? Pivot { get; set; }

	[JsonPropertyName("subgroups")]
	public List<FoodicsGroupInfoDto>? Subgroups { get; set; }

	[JsonPropertyName("deleted_at")]
	public string? DeletedAt { get; set; }
}

public class FoodicsGroupPivotDto
{
	[JsonPropertyName("is_active")]
	public bool? IsActive { get; set; }
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

public class FlexibleStringListJsonConverter : JsonConverter<List<string>?>
{
	public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return null;
		}

		var values = new List<string>();
		ReadValues(ref reader, values);
		return values;
	}

	public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
	{
		JsonSerializer.Serialize(writer, value, options);
	}

	private static void ReadValues(ref Utf8JsonReader reader, List<string> values)
	{
		switch (reader.TokenType)
		{
			case JsonTokenType.StartArray:
				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				{
					ReadValues(ref reader, values);
				}
				break;
			case JsonTokenType.StartObject:
				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					if (reader.TokenType != JsonTokenType.PropertyName)
					{
						continue;
					}

					var propertyName = reader.GetString();
					if (reader.Read())
					{
						if (reader.TokenType == JsonTokenType.String)
						{
							AddIfNotEmpty(values, reader.GetString());
						}
						else
						{
							AddIfNotEmpty(values, propertyName);
							ReadValues(ref reader, values);
						}
					}
				}
				break;
			case JsonTokenType.String:
				AddIfNotEmpty(values, reader.GetString());
				break;
			case JsonTokenType.Number:
				if (reader.TryGetInt64(out var longValue))
				{
					AddIfNotEmpty(values, longValue.ToString(CultureInfo.InvariantCulture));
				}
				else if (reader.TryGetDouble(out var doubleValue))
				{
					AddIfNotEmpty(values, doubleValue.ToString(CultureInfo.InvariantCulture));
				}
				break;
			case JsonTokenType.True:
			case JsonTokenType.False:
				AddIfNotEmpty(values, reader.GetBoolean().ToString(CultureInfo.InvariantCulture));
				break;
			default:
				break;
		}
	}

	private static void AddIfNotEmpty(List<string> values, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			values.Add(value);
		}
	}
}
