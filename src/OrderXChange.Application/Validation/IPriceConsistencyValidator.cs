using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;

namespace OrderXChange.Application.Validation;

/// <summary>
/// Validator for price consistency and validity
/// Ensures prices are valid, consistent, and within acceptable ranges
/// </summary>
public interface IPriceConsistencyValidator
{
    /// <summary>
    /// Validates price consistency for products
    /// </summary>
    Task<MenuValidationResult> ValidateAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates price for a single product
    /// </summary>
    Task<List<ValidationError>> ValidateProductPriceAsync(
        FoodicsProductDetailDto product,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates modifier option prices
    /// </summary>
    Task<List<ValidationError>> ValidateModifierPricesAsync(
        IEnumerable<FoodicsModifierDto> modifiers,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates price format and range
    /// </summary>
    Task<List<ValidationError>> ValidatePriceRangeAsync(
        decimal? price,
        string entityType,
        string entityId,
        string entityName,
        ValidationConfiguration config,
        CancellationToken cancellationToken = default);
}