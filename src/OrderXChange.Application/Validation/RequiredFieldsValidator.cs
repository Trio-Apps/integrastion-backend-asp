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
/// Validator implementation for required field checks
/// Ensures all mandatory fields are present and valid
/// </summary>
public class RequiredFieldsValidator : IRequiredFieldsValidator, ITransientDependency
{
    private readonly ILogger<RequiredFieldsValidator> _logger;

    public RequiredFieldsValidator(ILogger<RequiredFieldsValidator> logger)
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

        _logger.LogDebug("Validating required fields for {Count} products", productsList.Count);

        foreach (var product in productsList)
        {
            var productErrors = await ValidateProductAsync(product, config, cancellationToken);
            result.Errors.AddRange(productErrors);
        }

        // Validate categories
        var categories = productsList
            .Where(p => p.Category != null)
            .Select(p => p.Category!)
            .DistinctBy(c => c.Id)
            .ToList();

        var categoryErrors = await ValidateCategoriesAsync(categories, config, cancellationToken);
        result.Errors.AddRange(categoryErrors);

        // Validate modifiers
        var modifiers = productsList
            .Where(p => p.Modifiers != null)
            .SelectMany(p => p.Modifiers!)
            .DistinctBy(m => m.Id)
            .ToList();

        var modifierErrors = await ValidateModifiersAsync(modifiers, config, cancellationToken);
        result.Errors.AddRange(modifierErrors);

        result.IsValid = !result.BlockingErrors.Any();

        _logger.LogDebug(
            "Required fields validation completed. Errors={Errors}, Valid={Valid}",
            result.Errors.Count, result.IsValid);

        return result;
    }

    public async Task<List<ValidationError>> ValidateProductAsync(
        FoodicsProductDetailDto product,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        // Validate product ID
        if (string.IsNullOrWhiteSpace(product.Id))
        {
            errors.Add(ValidationError.Critical(
                ValidationErrorCode.MissingProductId,
                "Product ID is required",
                ValidationCategory.RequiredFields,
                "Product",
                product.Id,
                "Id"));
        }

        // Validate product name
        if (config.RequiredFields.Contains("Name") && string.IsNullOrWhiteSpace(product.Name))
        {
            var error = ValidationError.Critical(
                ValidationErrorCode.MissingProductName,
                "Product name is required",
                ValidationCategory.RequiredFields,
                "Product",
                product.Id,
                "Name");
            error.EntityName = product.Name;
            error.SuggestedFix = "Provide a valid product name";
            errors.Add(error);
        }

        // Validate product name length
        if (!string.IsNullOrWhiteSpace(product.Name) && product.Name.Length > config.MaxProductNameLength)
        {
            var error = ValidationError.Error(
                ValidationErrorCode.ExcessiveNameLength,
                $"Product name exceeds maximum length of {config.MaxProductNameLength} characters",
                ValidationCategory.TalabatCompliance,
                "Product",
                product.Id,
                "Name");
            error.EntityName = product.Name;
            error.CurrentValue = product.Name.Length;
            error.ExpectedValue = config.MaxProductNameLength;
            error.SuggestedFix = $"Truncate name to {config.MaxProductNameLength} characters or less";
            errors.Add(error);
        }

        // Validate price requirement
        if (config.RequiredFields.Contains("Price") && !product.Price.HasValue)
        {
            var error = ValidationError.Critical(
                ValidationErrorCode.MissingProductId, // Using existing code, should be MISSING_PRICE
                "Product price is required",
                ValidationCategory.RequiredFields,
                "Product",
                product.Id,
                "Price");
            error.EntityName = product.Name;
            error.SuggestedFix = "Provide a valid price greater than 0";
            errors.Add(error);
        }

        // Validate description length
        if (!string.IsNullOrWhiteSpace(product.Description) && product.Description.Length > config.MaxDescriptionLength)
        {
            var error = ValidationError.Error(
                ValidationErrorCode.ExcessiveDescriptionLength,
                $"Product description exceeds maximum length of {config.MaxDescriptionLength} characters",
                ValidationCategory.TalabatCompliance,
                "Product",
                product.Id,
                "Description");
            error.EntityName = product.Name;
            error.CurrentValue = product.Description.Length;
            error.ExpectedValue = config.MaxDescriptionLength;
            error.SuggestedFix = $"Truncate description to {config.MaxDescriptionLength} characters or less";
            errors.Add(error);
        }

        // Validate remote code requirement (for stable mapping)
        if (string.IsNullOrWhiteSpace(product.Id))
        {
            var error = ValidationError.Critical(
                ValidationErrorCode.MissingRemoteCode,
                "Product remote code (ID) is required for stable mapping",
                ValidationCategory.DataIntegrity,
                "Product",
                product.Id,
                "Id");
            error.EntityName = product.Name;
            error.SuggestedFix = "Ensure product has a valid Foodics ID";
            errors.Add(error);
        }

        // Validate invalid characters in name
        if (!string.IsNullOrWhiteSpace(product.Name) && ContainsInvalidCharacters(product.Name))
        {
            var error = ValidationError.Error(
                ValidationErrorCode.InvalidCharacters,
                "Product name contains invalid characters",
                ValidationCategory.TalabatCompliance,
                "Product",
                product.Id,
                "Name");
            error.EntityName = product.Name;
            error.CurrentValue = product.Name;
            error.SuggestedFix = "Remove special characters that may cause API issues";
            errors.Add(error);
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateCategoriesAsync(
        IEnumerable<FoodicsCategoryInfoDto> categories,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        foreach (var category in categories)
        {
            // Validate category ID
            if (string.IsNullOrWhiteSpace(category.Id))
            {
                errors.Add(ValidationError.Critical(
                    ValidationErrorCode.MissingRemoteCode,
                    "Category ID is required",
                    ValidationCategory.RequiredFields,
                    "Category",
                    category.Id,
                    "Id"));
            }

            // Validate category name
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                var error = ValidationError.Critical(
                    ValidationErrorCode.MissingCategoryName,
                    "Category name is required",
                    ValidationCategory.RequiredFields,
                    "Category",
                    category.Id,
                    "Name");
                error.EntityName = category.Name;
                error.SuggestedFix = "Provide a valid category name";
                errors.Add(error);
            }

            // Validate category name length
            if (!string.IsNullOrWhiteSpace(category.Name) && category.Name.Length > config.MaxProductNameLength)
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.ExcessiveNameLength,
                    $"Category name exceeds maximum length of {config.MaxProductNameLength} characters",
                    ValidationCategory.TalabatCompliance,
                    "Category",
                    category.Id,
                    "Name");
                error.EntityName = category.Name;
                error.CurrentValue = category.Name.Length;
                error.ExpectedValue = config.MaxProductNameLength;
                error.SuggestedFix = $"Truncate name to {config.MaxProductNameLength} characters or less";
                errors.Add(error);
            }

            // Validate invalid characters in category name
            if (!string.IsNullOrWhiteSpace(category.Name) && ContainsInvalidCharacters(category.Name))
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.InvalidCharacters,
                    "Category name contains invalid characters",
                    ValidationCategory.TalabatCompliance,
                    "Category",
                    category.Id,
                    "Name");
                error.EntityName = category.Name;
                error.CurrentValue = category.Name;
                error.SuggestedFix = "Remove special characters that may cause API issues";
                errors.Add(error);
            }
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateModifiersAsync(
        IEnumerable<FoodicsModifierDto> modifiers,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        foreach (var modifier in modifiers)
        {
            // Validate modifier ID
            if (string.IsNullOrWhiteSpace(modifier.Id))
            {
                errors.Add(ValidationError.Critical(
                    ValidationErrorCode.MissingRemoteCode,
                    "Modifier ID is required",
                    ValidationCategory.RequiredFields,
                    "Modifier",
                    modifier.Id,
                    "Id"));
            }

            // Validate modifier name
            if (string.IsNullOrWhiteSpace(modifier.Name))
            {
                var error = ValidationError.Critical(
                    ValidationErrorCode.MissingModifierName,
                    "Modifier name is required",
                    ValidationCategory.RequiredFields,
                    "Modifier",
                    modifier.Id,
                    "Name");
                error.EntityName = modifier.Name;
                error.SuggestedFix = "Provide a valid modifier name";
                errors.Add(error);
            }

            // Validate modifier name length
            if (!string.IsNullOrWhiteSpace(modifier.Name) && modifier.Name.Length > config.MaxProductNameLength)
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.ExcessiveNameLength,
                    $"Modifier name exceeds maximum length of {config.MaxProductNameLength} characters",
                    ValidationCategory.TalabatCompliance,
                    "Modifier",
                    modifier.Id,
                    "Name");
                error.EntityName = modifier.Name;
                error.CurrentValue = modifier.Name.Length;
                error.ExpectedValue = config.MaxProductNameLength;
                error.SuggestedFix = $"Truncate name to {config.MaxProductNameLength} characters or less";
                errors.Add(error);
            }

            // Validate modifier options
            if (modifier.Options != null)
            {
                foreach (var option in modifier.Options)
                {
                    // Validate option ID
                    if (string.IsNullOrWhiteSpace(option.Id))
                    {
                        errors.Add(ValidationError.Critical(
                            ValidationErrorCode.MissingRemoteCode,
                            "Modifier option ID is required",
                            ValidationCategory.RequiredFields,
                            "ModifierOption",
                            option.Id,
                            "Id"));
                    }

                    // Validate option name
                    if (string.IsNullOrWhiteSpace(option.Name))
                    {
                        var error = ValidationError.Error(
                            ValidationErrorCode.MissingModifierName, // Reusing code
                            "Modifier option name is required",
                            ValidationCategory.RequiredFields,
                            "ModifierOption",
                            option.Id,
                            "Name");
                        error.EntityName = option.Name;
                        error.SuggestedFix = "Provide a valid option name";
                        errors.Add(error);
                    }

                    // Validate option name length
                    if (!string.IsNullOrWhiteSpace(option.Name) && option.Name.Length > config.MaxProductNameLength)
                    {
                        var error = ValidationError.Error(
                            ValidationErrorCode.ExcessiveNameLength,
                            $"Modifier option name exceeds maximum length of {config.MaxProductNameLength} characters",
                            ValidationCategory.TalabatCompliance,
                            "ModifierOption",
                            option.Id,
                            "Name");
                        error.EntityName = option.Name;
                        error.CurrentValue = option.Name.Length;
                        error.ExpectedValue = config.MaxProductNameLength;
                        error.SuggestedFix = $"Truncate name to {config.MaxProductNameLength} characters or less";
                        errors.Add(error);
                    }
                }
            }
        }

        return errors;
    }

    #region Private Methods

    private static bool ContainsInvalidCharacters(string text)
    {
        // Check for characters that might cause issues with Talabat API
        // This is a basic implementation - expand based on actual API requirements
        var invalidChars = new[] { '<', '>', '"', '\'', '&', '\n', '\r', '\t' };
        return text.IndexOfAny(invalidChars) >= 0;
    }

    #endregion
}