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
/// Validator implementation for modifier correctness and structure
/// Ensures modifiers have valid options, selections, and relationships
/// </summary>
public class ModifierCorrectnessValidator : IModifierCorrectnessValidator, ITransientDependency
{
    private readonly ILogger<ModifierCorrectnessValidator> _logger;

    public ModifierCorrectnessValidator(ILogger<ModifierCorrectnessValidator> logger)
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

        _logger.LogDebug("Validating modifier correctness for {Count} products", productsList.Count);

        foreach (var product in productsList)
        {
            var productErrors = await ValidateProductModifiersAsync(product, config, cancellationToken);
            result.Errors.AddRange(productErrors);
        }

        result.IsValid = !result.BlockingErrors.Any();

        _logger.LogDebug(
            "Modifier correctness validation completed. Errors={Errors}, Valid={Valid}",
            result.Errors.Count, result.IsValid);

        return result;
    }

    public async Task<List<ValidationError>> ValidateProductModifiersAsync(
        FoodicsProductDetailDto product,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        if (product.Modifiers == null || product.Modifiers.Count == 0)
        {
            // No modifiers is valid
            return errors;
        }

        // Check for duplicate modifier IDs within the same product
        var modifierIds = product.Modifiers.Select(m => m.Id).ToList();
        var duplicateIds = modifierIds.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicateId in duplicateIds)
        {
            var error = ValidationError.Error(
                ValidationErrorCode.DuplicateRemoteCode,
                $"Duplicate modifier ID '{duplicateId}' found in product",
                ValidationCategory.DataIntegrity,
                "Product",
                product.Id,
                "Modifiers");
            error.EntityName = product.Name;
            error.CurrentValue = duplicateId;
            error.SuggestedFix = "Remove duplicate modifier or ensure unique IDs";
            errors.Add(error);
        }

        // Validate each modifier
        foreach (var modifier in product.Modifiers)
        {
            var modifierErrors = await ValidateModifierAsync(modifier, config, cancellationToken);
            errors.AddRange(modifierErrors);
        }

        // Check for excessive number of modifiers
        if (product.Modifiers.Count > 20) // Reasonable limit
        {
            var error = ValidationError.Error(
                ValidationErrorCode.ExcessiveModifierOptions,
                $"Product has {product.Modifiers.Count} modifiers, which may impact performance",
                ValidationCategory.Performance,
                "Product",
                product.Id,
                "Modifiers");
            error.EntityName = product.Name;
            error.CurrentValue = product.Modifiers.Count;
            error.ExpectedValue = "≤ 20";
            error.SuggestedFix = "Consider consolidating modifiers or splitting into multiple products";
            errors.Add(error);
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateModifierAsync(
        FoodicsModifierDto modifier,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        // Validate modifier has options
        if (modifier.Options == null || modifier.Options.Count == 0)
        {
            var error = ValidationError.Critical(
                ValidationErrorCode.ModifierWithoutOptions,
                "Modifier must have at least one option",
                ValidationCategory.ModifierCorrectness,
                "Modifier",
                modifier.Id,
                "Options");
            error.EntityName = modifier.Name;
            error.SuggestedFix = "Add at least one option to the modifier or remove the modifier";
            errors.Add(error);
            return errors; // Can't validate further without options
        }

        // Validate selection rules
        var selectionErrors = await ValidateModifierSelectionRulesAsync(modifier, config, cancellationToken);
        errors.AddRange(selectionErrors);

        // Validate options
        var optionErrors = await ValidateModifierOptionsAsync(modifier.Options, modifier.Id, modifier.Name ?? "", config, cancellationToken);
        errors.AddRange(optionErrors);

        // Check for excessive number of options
        if (modifier.Options.Count > config.MaxModifierOptions)
        {
            var error = ValidationError.Error(
                ValidationErrorCode.ExcessiveModifierOptions,
                $"Modifier has {modifier.Options.Count} options, exceeding maximum of {config.MaxModifierOptions}",
                ValidationCategory.ModifierCorrectness,
                "Modifier",
                modifier.Id,
                "Options");
            error.EntityName = modifier.Name;
            error.CurrentValue = modifier.Options.Count;
            error.ExpectedValue = config.MaxModifierOptions;
            error.SuggestedFix = $"Reduce options to {config.MaxModifierOptions} or less, or split into multiple modifiers";
            errors.Add(error);
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateModifierSelectionRulesAsync(
        FoodicsModifierDto modifier,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        await Task.CompletedTask; // Placeholder for async operations

        var optionCount = modifier.Options?.Count ?? 0;

        // Validate MinAllowed
        if (modifier.MinAllowed.HasValue)
        {
            var minAllowed = modifier.MinAllowed.Value;

            if (minAllowed < 0)
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.InvalidModifierSelection,
                    "Modifier MinAllowed cannot be negative",
                    ValidationCategory.ModifierCorrectness,
                    "Modifier",
                    modifier.Id,
                    "MinAllowed");
                error.EntityName = modifier.Name;
                error.CurrentValue = minAllowed;
                error.ExpectedValue = "≥ 0";
                error.SuggestedFix = "Set MinAllowed to 0 or a positive value";
                errors.Add(error);
            }

            if (minAllowed > optionCount)
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.InvalidModifierSelection,
                    $"Modifier MinAllowed ({minAllowed}) cannot exceed number of options ({optionCount})",
                    ValidationCategory.ModifierCorrectness,
                    "Modifier",
                    modifier.Id,
                    "MinAllowed");
                error.EntityName = modifier.Name;
                error.CurrentValue = minAllowed;
                error.ExpectedValue = $"≤ {optionCount}";
                error.SuggestedFix = $"Set MinAllowed to {optionCount} or less";
                errors.Add(error);
            }
        }

        // Validate MaxAllowed
        if (modifier.MaxAllowed.HasValue)
        {
            var maxAllowed = modifier.MaxAllowed.Value;

            if (maxAllowed <= 0)
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.InvalidModifierSelection,
                    "Modifier MaxAllowed must be greater than 0",
                    ValidationCategory.ModifierCorrectness,
                    "Modifier",
                    modifier.Id,
                    "MaxAllowed");
                error.EntityName = modifier.Name;
                error.CurrentValue = maxAllowed;
                error.ExpectedValue = "> 0";
                error.SuggestedFix = "Set MaxAllowed to 1 or higher";
                errors.Add(error);
            }

            if (maxAllowed > optionCount)
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.InvalidModifierSelection,
                    $"Modifier MaxAllowed ({maxAllowed}) cannot exceed number of options ({optionCount})",
                    ValidationCategory.ModifierCorrectness,
                    "Modifier",
                    modifier.Id,
                    "MaxAllowed");
                error.EntityName = modifier.Name;
                error.CurrentValue = maxAllowed;
                error.ExpectedValue = $"≤ {optionCount}";
                error.SuggestedFix = $"Set MaxAllowed to {optionCount} or less";
                errors.Add(error);
            }
        }

        // Validate MinAllowed vs MaxAllowed
        if (modifier.MinAllowed.HasValue && modifier.MaxAllowed.HasValue)
        {
            var minAllowed = modifier.MinAllowed.Value;
            var maxAllowed = modifier.MaxAllowed.Value;

            if (minAllowed > maxAllowed)
            {
                var error = ValidationError.Error(
                    ValidationErrorCode.InvalidModifierSelection,
                    $"Modifier MinAllowed ({minAllowed}) cannot be greater than MaxAllowed ({maxAllowed})",
                    ValidationCategory.ModifierCorrectness,
                    "Modifier",
                    modifier.Id,
                    "Selection");
                error.EntityName = modifier.Name;
                error.CurrentValue = $"Min: {minAllowed}, Max: {maxAllowed}";
                error.ExpectedValue = "Min ≤ Max";
                error.SuggestedFix = "Adjust MinAllowed and MaxAllowed so Min ≤ Max";
                errors.Add(error);
            }
        }

        // Validate required modifier (MinAllowed > 0 indicates required)
        if (modifier.MinAllowed.HasValue && modifier.MinAllowed.Value > 0 && optionCount == 0)
        {
            var error = ValidationError.Error(
                ValidationErrorCode.RequiredModifierNotSelected,
                "Required modifier (MinAllowed > 0) has no options available",
                ValidationCategory.BusinessRules,
                "Modifier",
                modifier.Id,
                "MinAllowed");
            error.EntityName = modifier.Name;
            error.CurrentValue = $"MinAllowed: {modifier.MinAllowed.Value}, Options: 0";
            error.ExpectedValue = "MinAllowed: > 0, Options: > 0";
            error.SuggestedFix = "Add options to the modifier or set MinAllowed to 0";
            errors.Add(error);
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateModifierOptionsAsync(
        IEnumerable<FoodicsModifierOptionDto> options,
        string modifierId,
        string modifierName,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();
        var optionsList = options.ToList();

        await Task.CompletedTask; // Placeholder for async operations

        // Check for duplicate option IDs
        var optionIds = optionsList.Select(o => o.Id).ToList();
        var duplicateIds = optionIds.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicateId in duplicateIds)
        {
            var error = ValidationError.Error(
                ValidationErrorCode.DuplicateModifierOption,
                $"Duplicate modifier option ID '{duplicateId}' found in modifier '{modifierName}'",
                ValidationCategory.DataIntegrity,
                "Modifier",
                modifierId,
                "Options");
            error.EntityName = modifierName;
            error.CurrentValue = duplicateId;
            error.SuggestedFix = "Ensure all modifier options have unique IDs";
            errors.Add(error);
        }

        // Check for duplicate option names (warning)
        var optionNames = optionsList.Where(o => !string.IsNullOrWhiteSpace(o.Name)).Select(o => o.Name!).ToList();
        var duplicateNames = optionNames.GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicateName in duplicateNames)
        {
            errors.Add(ValidationError.Create(
                ValidationErrorCode.DuplicateModifierOption,
                $"Duplicate modifier option name '{duplicateName}' found in modifier '{modifierName}' - this may confuse customers",
                ValidationCategory.BusinessRules,
                "Modifier",
                modifierId,
                "Consider using unique names for better customer experience"));
        }

        // Validate individual options
        foreach (var option in optionsList)
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
                    ValidationErrorCode.MissingModifierName,
                    "Modifier option name is required",
                    ValidationCategory.RequiredFields,
                    "ModifierOption",
                    option.Id,
                    "Name");
                error.EntityName = option.Name;
                error.SuggestedFix = "Provide a descriptive name for the modifier option";
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

        // Check if all options are inactive (warning)
        // In Foodics, an option is considered active if it has branches
        var activeOptions = optionsList.Where(o => o.Branches != null && o.Branches.Any()).ToList();
        if (optionsList.Any() && !activeOptions.Any())
        {
            errors.Add(ValidationError.Create(
                "ALL_OPTIONS_INACTIVE",
                $"All options in modifier '{modifierName}' have no branches assigned - customers cannot make selections",
                ValidationCategory.BusinessRules,
                "Modifier",
                modifierId,
                "Assign branches to at least one option or remove the modifier"));
        }

        return errors;
    }
}
