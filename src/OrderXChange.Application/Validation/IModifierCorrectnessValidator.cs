using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;

namespace OrderXChange.Application.Validation;

/// <summary>
/// Validator for modifier correctness and structure
/// Ensures modifiers have valid options, selections, and relationships
/// </summary>
public interface IModifierCorrectnessValidator
{
    /// <summary>
    /// Validates modifier correctness for products
    /// </summary>
    Task<MenuValidationResult> ValidateAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates modifiers for a single product
    /// </summary>
    Task<List<ValidationError>> ValidateProductModifiersAsync(
        FoodicsProductDetailDto product,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a single modifier structure
    /// </summary>
    Task<List<ValidationError>> ValidateModifierAsync(
        FoodicsModifierDto modifier,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates modifier selection rules
    /// </summary>
    Task<List<ValidationError>> ValidateModifierSelectionRulesAsync(
        FoodicsModifierDto modifier,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates modifier options
    /// </summary>
    Task<List<ValidationError>> ValidateModifierOptionsAsync(
        IEnumerable<FoodicsModifierOptionDto> options,
        string modifierId,
        string modifierName,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);
}