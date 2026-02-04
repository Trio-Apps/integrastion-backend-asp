using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Staging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Staging;

/// <summary>
/// Converts FoodicsProductStaging entities back to FoodicsProductDetailDto objects
/// This allows us to submit from staging table to Talabat
/// </summary>
public class FoodicsProductStagingToFoodicsConverter : ITransientDependency
{
	private readonly ILogger<FoodicsProductStagingToFoodicsConverter> _logger;

	public FoodicsProductStagingToFoodicsConverter(ILogger<FoodicsProductStagingToFoodicsConverter> logger)
	{
		_logger = logger;
	}

	/// <summary>
	/// Converts a list of staging products to Foodics DTOs with all details
	/// </summary>
	public List<FoodicsProductDetailDto> ConvertToFoodicsDto(List<FoodicsProductStaging> stagingProducts)
	{
		var result = new List<FoodicsProductDetailDto>();

		foreach (var stagingProduct in stagingProducts)
		{
			try
			{
				var foodicsDto = ConvertSingleProduct(stagingProduct);
				result.Add(foodicsDto);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Failed to convert staging product {StagingProductId} (FoodicsProductId: {FoodicsProductId}) to DTO. Error: {Error}",
					stagingProduct.Id,
					stagingProduct.FoodicsProductId,
					ex.Message);
				// Continue with next product
			}
		}

		_logger.LogInformation(
			"Converted {ConvertedCount}/{TotalCount} staging products to Foodics DTOs",
			result.Count,
			stagingProducts.Count);

		return result;
	}

	/// <summary>
	/// Converts a single staging product to FoodicsProductDetailDto
	/// </summary>
	private FoodicsProductDetailDto ConvertSingleProduct(FoodicsProductStaging staging)
	{
		return new FoodicsProductDetailDto
		{
			Id = staging.FoodicsProductId,
			Name = staging.Name,
			NameLocalized = staging.NameLocalized,
			Description = staging.Description,
			DescriptionLocalized = staging.DescriptionLocalized,
			Image = staging.Image,
			IsActive = staging.IsActive,
			Sku = staging.Sku,
			Barcode = staging.Barcode,
			CategoryId = staging.CategoryId,
			TaxGroupId = staging.TaxGroupId,
			Price = staging.Price,

			// Deserialize category information
			Category = DeserializeCategory(staging),

			// Deserialize tax group
			TaxGroup = DeserializeTaxGroup(staging),

			// Deserialize complex JSON fields
			PriceTags = DeserializeJson<List<FoodicsPriceTagDto>>(staging.PriceTagsJson),
			Tags = DeserializeJson<List<FoodicsTagDto>>(staging.TagsJson),
			Branches = DeserializeJson<List<FoodicsBranchDto>>(staging.BranchesJson),
			Ingredients = DeserializeJson<List<FoodicsIngredientDto>>(staging.IngredientsJson),
			Modifiers = DeserializeJson<List<FoodicsModifierDto>>(staging.ModifiersJson),
			Groups = DeserializeJson<List<FoodicsGroupInfoDto>>(staging.GroupsJson),
			Discounts = DeserializeJson<List<FoodicsDiscountDto>>(staging.DiscountsJson),
			TimedEvents = DeserializeJson<List<FoodicsTimedEventDto>>(staging.TimedEventsJson)
		};
	}

	/// <summary>
	/// Deserializes category from staging product
	/// </summary>
	private FoodicsCategoryInfoDto? DeserializeCategory(FoodicsProductStaging staging)
	{
		if (string.IsNullOrWhiteSpace(staging.CategoryId))
		{
			return null;
		}

		// If we have full category details in ProductDetailsJson, use that
		if (!string.IsNullOrWhiteSpace(staging.ProductDetailsJson))
		{
			try
			{
				var productDetails = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(staging.ProductDetailsJson);
				if (productDetails != null && productDetails.TryGetValue("Category", out var categoryElement))
				{
					return JsonSerializer.Deserialize<FoodicsCategoryInfoDto>(categoryElement.GetRawText());
				}
			}
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Could not deserialize category from ProductDetailsJson for product {ProductId}", staging.FoodicsProductId);
			}
		}

		// Fallback: Create basic category from denormalized fields
		return new FoodicsCategoryInfoDto
		{
			Id = staging.CategoryId,
			Name = staging.CategoryName ?? $"Category-{staging.CategoryId}"
		};
	}

	/// <summary>
	/// Deserializes tax group from staging product
	/// </summary>
	private FoodicsTaxGroupDto? DeserializeTaxGroup(FoodicsProductStaging staging)
	{
		if (string.IsNullOrWhiteSpace(staging.TaxGroupId))
		{
			return null;
		}

		// If we have full tax group details in ProductDetailsJson, use that
		if (!string.IsNullOrWhiteSpace(staging.ProductDetailsJson))
		{
			try
			{
				var productDetails = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(staging.ProductDetailsJson);
				if (productDetails != null && productDetails.TryGetValue("TaxGroup", out var taxGroupElement))
				{
					return JsonSerializer.Deserialize<FoodicsTaxGroupDto>(taxGroupElement.GetRawText());
				}
			}
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Could not deserialize tax group from ProductDetailsJson for product {ProductId}", staging.FoodicsProductId);
			}
		}

		// Fallback: Create basic tax group from denormalized fields
		return new FoodicsTaxGroupDto
		{
			Id = staging.TaxGroupId,
			Name = staging.TaxGroupName ?? $"TaxGroup-{staging.TaxGroupId}",
			Rate = staging.TaxRate ?? 0
		};
	}

	/// <summary>
	/// Generic JSON deserialization with error handling
	/// </summary>
	private T? DeserializeJson<T>(string? json) where T : class
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
		}
		catch (Exception ex)
		{
			_logger.LogDebug(ex, "Failed to deserialize JSON to {Type}. JSON length: {Length}", typeof(T).Name, json.Length);
			return null;
		}
	}
}

