using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Versioning.DTOs;

namespace OrderXChange.Application.Validation;

/// <summary>
/// Extension methods for integrating validation with existing sync services
/// Provides convenient validation methods for common scenarios
/// </summary>
public static class ValidationIntegrationExtensions
{
    /// <summary>
    /// Validates products and returns only valid ones for sync
    /// Logs validation issues for monitoring
    /// </summary>
    public static async Task<(List<FoodicsProductDetailDto> ValidProducts, MenuValidationResult ValidationResult)> 
        ValidateAndFilterAsync(
            this IMenuValidationService validationService,
            IEnumerable<FoodicsProductDetailDto> products,
            Guid foodicsAccountId,
            string? branchId = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
    {
        var productsList = products.ToList();
        var validationResult = await validationService.ValidateMenuAsync(
            productsList, foodicsAccountId, branchId, failFast: false, cancellationToken);

        // Get IDs of products with critical errors
        var invalidProductIds = validationResult.CriticalErrors
            .Where(e => e.EntityType == "Product" && !string.IsNullOrEmpty(e.EntityId))
            .Select(e => e.EntityId!)
            .ToHashSet();

        // Filter out invalid products
        var validProducts = productsList
            .Where(p => !invalidProductIds.Contains(p.Id))
            .ToList();

        // Log validation summary
        if (logger != null)
        {
            if (validationResult.CriticalErrors.Any())
            {
                logger.LogWarning(
                    "Menu validation found {CriticalErrors} critical errors, {Errors} errors, {Warnings} warnings. " +
                    "Filtered out {InvalidCount} invalid products. Valid products: {ValidCount}",
                    validationResult.CriticalErrors.Count,
                    validationResult.Errors.Count,
                    validationResult.Warnings.Count,
                    invalidProductIds.Count,
                    validProducts.Count);
            }
            else if (validationResult.Warnings.Any())
            {
                logger.LogInformation(
                    "Menu validation completed with {Warnings} warnings. All {ProductCount} products are valid.",
                    validationResult.Warnings.Count,
                    validProducts.Count);
            }
            else
            {
                logger.LogInformation(
                    "Menu validation passed with no issues. {ProductCount} products validated.",
                    validProducts.Count);
            }
        }

        return (validProducts, validationResult);
    }

    /// <summary>
    /// Quick validation check for critical errors only
    /// Returns true if menu can be submitted to Talabat
    /// </summary>
    public static async Task<bool> CanSubmitToTalabatAsync(
        this IMenuValidationService validationService,
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validationService.ValidateMenuAsync(
            products, foodicsAccountId, branchId, failFast: true, cancellationToken);

        return validationResult.CanSubmitToTalabat;
    }

    /// <summary>
    /// Validates delta payload and returns detailed error information
    /// </summary>
    public static async Task<(bool IsValid, string ErrorSummary)> ValidateDeltaQuickAsync(
        this IMenuValidationService validationService,
        MenuDeltaPayload deltaPayload,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validationService.ValidateDeltaAsync(
            deltaPayload, failFast: true, cancellationToken);

        var errorSummary = validationResult.IsValid 
            ? "Validation passed" 
            : validationResult.GetSummary();

        return (validationResult.CanSubmitToTalabat, errorSummary);
    }

    /// <summary>
    /// Gets validation summary for reporting and monitoring
    /// </summary>
    public static ValidationSummary GetValidationSummary(this MenuValidationResult validationResult)
    {
        return new ValidationSummary
        {
            IsValid = validationResult.IsValid,
            CanSubmitToTalabat = validationResult.CanSubmitToTalabat,
            TotalErrors = validationResult.Errors.Count,
            CriticalErrors = validationResult.CriticalErrors.Count,
            TotalWarnings = validationResult.Warnings.Count,
            ValidationTime = validationResult.ValidationDuration,
            ErrorsByCategory = validationResult.Errors
                .GroupBy(e => e.Category)
                .ToDictionary(g => g.Key, g => g.Count()),
            WarningsByCategory = validationResult.Warnings
                .GroupBy(w => w.Category)
                .ToDictionary(g => g.Key, g => g.Count()),
            Summary = validationResult.GetSummary()
        };
    }
}

/// <summary>
/// Validation summary for reporting and monitoring
/// </summary>
public class ValidationSummary
{
    public bool IsValid { get; set; }
    public bool CanSubmitToTalabat { get; set; }
    public int TotalErrors { get; set; }
    public int CriticalErrors { get; set; }
    public int TotalWarnings { get; set; }
    public TimeSpan ValidationTime { get; set; }
    public Dictionary<string, int> ErrorsByCategory { get; set; } = new();
    public Dictionary<string, int> WarningsByCategory { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}