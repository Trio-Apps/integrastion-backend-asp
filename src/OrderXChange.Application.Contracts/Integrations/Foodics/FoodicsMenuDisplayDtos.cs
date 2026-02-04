using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsMenuDisplayResponseDto
{
	public FoodicsMenuDisplayDataDto Data { get; set; } = new();
}

public class FoodicsMenuDisplayDataDto
{
	[JsonPropertyName("categories")]
	public List<FoodicsMenuDisplayCategoryDto> Categories { get; set; } = [];

	// The "custom" section contains groups defined by the business that can
	// appear on the menu display similar to categories.
	[JsonPropertyName("custom")]
	public List<FoodicsMenuDisplayGroupDto>? Custom { get; set; }
}

public class FoodicsMenuDisplayCategoryDto
{
	// In menu_display payload, the key is "category_id" not "id"
	[JsonPropertyName("category_id")]
	public string CategoryId { get; set; } = string.Empty;

	[JsonPropertyName("children")]
	public List<FoodicsMenuDisplayChildDto> Children { get; set; } = [];
}

public class FoodicsMenuDisplayGroupDto
{
	[JsonPropertyName("group_id")]
	public string GroupId { get; set; } = string.Empty;

	[JsonPropertyName("children")]
	public List<FoodicsMenuDisplayChildDto> Children { get; set; } = [];
}

public class FoodicsMenuDisplayChildDto
{
	// child_type: "product" | "category" | "group" depending on the node
	[JsonPropertyName("child_type")]
	public string ChildType { get; set; } = string.Empty;

	[JsonPropertyName("child_id")]
	public string ChildId { get; set; } = string.Empty;

	// Nested children are supported for hierarchical menus
	[JsonPropertyName("children")]
	public List<FoodicsMenuDisplayChildDto>? Children { get; set; }
}


