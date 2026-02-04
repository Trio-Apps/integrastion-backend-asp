using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;

namespace OrderXChange.Application.Validation;

/// <summary>
/// Validator for required field checks
/// Ensures all mandatory fields are present and valid
/// </summary>
public interface IRequiredFieldsValidator
{
    /// <summary>
    /// Validates required fields for products
    /// </summary>
    Task<MenuValidationResult> ValidateAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates required fields for a single product
    /// </summary>
    Task<List<ValidationError>> ValidateProductAsync(
        FoodicsProductDetailDto product,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates required fields for categories
    /// </summary>
    Task<List<ValidationError>> ValidateCategoriesAsync(
        IEnumerable<FoodicsCategoryInfoDto> categories,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates required fields for modifiers
    /// </summary>
    Task<List<ValidationError>> ValidateModifiersAsync(
        IEnumerable<FoodicsModifierDto> modifiers,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);
}