using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Validation;

/// <summary>
/// Validator implementation for price consistency and validity
/// Ensures prices are valid, consistent, and within acceptable ranges
/// </summary>
public class PriceConsistencyValidator : IPriceConsistencyValidator, ITransientDependency
{
    private readonly ILogger<PriceConsistencyValidator> _logger;

    public PriceConsistencyValidator(ILogger<PriceConsistencyValidator> logger)
    {
        _logger = logger;
    }

    public async Task<MenuValidationResult> ValidateAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var result = new MenuValidationResult { IsValid = true };
        var productsList = products.ToList();

        _logger.LogDebug("Validating price consistency for {Count} products", productsList.Count);

        foreach (var product in productsList)
        {
            var productErrors = await ValidateProductPriceAsync(product, config, cancellationToken);
            result.Errors.AddRange(productErrors);
        }

        // Validate modifier prices
        var modifiers = productsList
            .Where(p => p.Modifiers != null)
            .SelectMany(p => p.Modifiers!)
            .DistinctBy(m => m.Id)
            .ToList();

        var modifierErrors = await ValidateModifierPricesAsync(modifiers, config, cancellationToken);
        result.Errors.AddRange(modifierErrors);

        result.IsValid = !result.BlockingErrors.Any();

        _logger.LogDebug(
            "Price consistency validation completed. Errors={Errors}, Valid={Valid}",
            result.Errors.Count, result.IsValid);

        return result;
    }

    public async Task<List<ValidationError>> ValidateProductPriceAsync(
        FoodicsProductDetailDto product,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        // Validate price range
        var priceErrors = await ValidatePriceRangeAsync(
            product.Price, "Product", product.Id, product.Name ?? "", config, cancellationToken);
        errors.AddRange(priceErrors);

        // Validate price format (decimal precision)
        if (product.Price.HasValue && HasExcessiveDecimalPlaces(product.Price.Value))
        {
            var error = ValidationError.Error(
                ValidationErrorCode.InvalidPriceFormat,
                "Product price has too many decimal places (maximum 2 allowed)",
                ValidationCategory.PriceConsistency,
                "Product",
                product.Id,
                "Price");
            error.EntityName = product.Name;
            error.CurrentValue = product.Price.Value;
            error.ExpectedValue = Math.Round(product.Price.Value, 2);
            error.SuggestedFix = "Round price to 2 decimal places";
            errors.Add(error);
        }

        // Validate price consistency with tax
        if (product.Price.HasValue && product.TaxGroup?.Rate.HasValue == true)
        {
            var taxRate = product.TaxGroup.Rate.Value;
            if (taxRate < 0 || taxRate > 100)
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.InvalidPriceFormat,
                    $"Invalid tax rate: {taxRate}%. Tax rate should be between 0% and 100%",
                    ValidationCategory.PriceConsistency,
                    "Product",
                    product.Id,
                    "TaxRate");
                error.EntityName = product.Name;
                error.CurrentValue = taxRate;
                error.ExpectedValue = "0-100";
                error.SuggestedFix = "Ensure tax rate is between 0% and 100%";
                errors.Add(error);
            }
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateModifierPricesAsync(
        IEnumerable<FoodicsModifierDto> modifiers,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        foreach (var modifier in modifiers)
        {
            if (modifier.Options == null) continue;

            foreach (var option in modifier.Options)
            {
                // Validate modifier option price range
                var priceErrors = await ValidatePriceRangeAsync(
                    option.Price, "ModifierOption", option.Id, option.Name ?? "", config, cancellationToken);
                errors.AddRange(priceErrors);

                // Validate price format
                if (option.Price.HasValue && HasExcessiveDecimalPlaces(option.Price.Value))
                {
                    var error = ValidationError.Error(
                        ValidationErrorCode.InvalidPriceFormat,
                        "Modifier option price has too many decimal places (maximum 2 allowed)",
                        ValidationCategory.PriceConsistency,
                        "ModifierOption",
                        option.Id,
                        "Price");
                    error.EntityName = option.Name;
                    error.CurrentValue = option.Price.Value;
                    error.ExpectedValue = Math.Round(option.Price.Value, 2);
                    error.SuggestedFix = "Round price to 2 decimal places";
                    errors.Add(error);
                }
            }

            // Validate modifier price consistency
            var modifierPriceErrors = await ValidateModifierPriceConsistencyAsync(modifier, config, cancellationToken);
            errors.AddRange(modifierPriceErrors);
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidatePriceRangeAsync(
        decimal? price,
        string entityType,
        string entityId,
        string entityName,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        if (!price.HasValue)
        {
            // Price is optional for some entities (like modifier options)
            return errors;
        }

        var priceValue = price.Value;

        // Check for negative price
        if (priceValue < 0)
        {
            var error = ValidationError.Critical(
                ValidationErrorCode.NegativePrice,
                $"{entityType} price cannot be negative",
                ValidationCategory.PriceConsistency,
                entityType,
                entityId,
                "Price");
            error.EntityName = entityName;
            error.CurrentValue = priceValue;
            error.ExpectedValue = "≥ 0";
            error.SuggestedFix = "Set price to 0 or a positive value";
            errors.Add(error);
        }

        // Check for zero price (warning for products, allowed for modifier options)
        if (priceValue == 0 && entityType == "Product")
        {
            var error = ValidationError.Error(
                ValidationErrorCode.ZeroPrice,
                "Product price is zero - this may indicate a pricing issue",
                ValidationCategory.PriceConsistency,
                entityType,
                entityId,
                "Price");
            error.EntityName = entityName;
            error.CurrentValue = priceValue;
            error.ExpectedValue = $"> {config.MinProductPrice}";
            error.SuggestedFix = "Verify if zero price is intentional or set appropriate price";
            errors.Add(error);
        }

        // Check minimum price for products
        if (entityType == "Product" && priceValue > 0 && priceValue < config.MinProductPrice)
        {
            var error = ValidationError.Error(
                ValidationErrorCode.InvalidPriceFormat,
                $"Product price is below minimum allowed price of {config.MinProductPrice:C}",
                ValidationCategory.PriceConsistency,
                entityType,
                entityId,
                "Price");
            error.EntityName = entityName;
            error.CurrentValue = priceValue;
            error.ExpectedValue = config.MinProductPrice;
            error.SuggestedFix = $"Set price to at least {config.MinProductPrice:C}";
            errors.Add(error);
        }

        // Check maximum price
        if (priceValue > config.MaxProductPrice)
        {
            var error = ValidationError.Error(
                ValidationErrorCode.ExcessivePrice,
                $"{entityType} price exceeds maximum allowed price of {config.MaxProductPrice:C}",
                ValidationCategory.PriceConsistency,
                entityType,
                entityId,
                "Price");
            error.EntityName = entityName;
            error.CurrentValue = priceValue;
            error.ExpectedValue = config.MaxProductPrice;
            error.SuggestedFix = $"Set price to at most {config.MaxProductPrice:C} or verify if this is correct";
            errors.Add(error);
        }

        return errors;
    }

    #region Private Methods

    private async Task<List<ValidationError>> ValidateModifierPriceConsistencyAsync(
        FoodicsModifierDto modifier,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        if (modifier.Options == null || modifier.Options.Count == 0)
            return errors;

        // Check for price inconsistencies within modifier options
        var optionsWithPrices = modifier.Options.Where(o => o.Price.HasValue).ToList();
        
        if (optionsWithPrices.Count > 1)
        {
            var prices = optionsWithPrices.Select(o => o.Price!.Value).ToList();
            var minPrice = prices.Min();
            var maxPrice = prices.Max();
            var priceRange = maxPrice - minPrice;

            // Flag if there's a very large price range within the same modifier
            // This might indicate data entry errors
            if (priceRange > config.MaxProductPrice * 0.5m) // 50% of max price
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.ModifierPriceInconsistency,
                    $"Large price variation in modifier options (range: {priceRange:C}). This may indicate data entry errors.",
                    ValidationCategory.PriceConsistency,
                    "Modifier",
                    modifier.Id,
                    "Options");
                error.EntityName = modifier.Name;
                error.CurrentValue = priceRange;
                error.ExpectedValue = "Consistent pricing";
                error.SuggestedFix = "Review modifier option prices for consistency";
                errors.Add(error);
            }
        }

        // Check for required modifiers with all zero-price options
        // A modifier is required if MinAllowed > 0
        if (modifier.MinAllowed.HasValue && modifier.MinAllowed.Value > 0 && optionsWithPrices.All(o => o.Price == 0))
        {
            // This is actually valid - required modifiers can have free options
            // But we'll add a warning for review
            errors.Add(ValidationError.Create(
                "REQUIRED_MODIFIER_ALL_FREE",
                "Required modifier has all free options - verify this is intentional",
                ValidationCategory.BusinessRules,
                "Modifier",
                modifier.Id,
                "Review pricing strategy for required modifier"));
        }

        return errors;
    }

    private static bool HasExcessiveDecimalPlaces(decimal price)
    {
        // Check if price has more than 2 decimal places
        var rounded = Math.Round(price, 2);
        return price != rounded;
    }

    #endregion
}