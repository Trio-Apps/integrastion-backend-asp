using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Versioning.DTOs;

namespace OrderXChange.Application.Validation;

/// <summary>
/// Main validation service interface for menu validation pipeline
/// Coordinates all validation layers with fail-fast strategy
/// </summary>
public interface IMenuValidationService
{
    /// <summary>
    /// Validates a complete menu before sending to Talabat
    /// Uses fail-fast strategy - stops on critical errors
    /// </summary>
    Task<MenuValidationResult> ValidateMenuAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId = null,
        bool failFast = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a delta payload before sending to Talabat
    /// Focuses on changed items only for performance
    /// </summary>
    Task<MenuValidationResult> ValidateDeltaAsync(
        MenuDeltaPayload deltaPayload,
        bool failFast = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates specific products for targeted validation
    /// </summary>
    Task<MenuValidationResult> ValidateProductsAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets validation configuration for an account/branch
    /// </summary>
    Task<ValidationConfiguration> GetValidationConfigurationAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validation options for customizing validation behavior
/// </summary>
public class ValidationOptions
{
    public bool FailFast { get; set; } = true;
    public bool SkipWarnings { get; set; } = false;
    public List<string> SkipValidationCategories { get; set; } = new();
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Validation configuration for account/branch specific rules
/// </summary>
public class ValidationConfiguration
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public decimal MaxProductPrice { get; set; } = 1000m;
    public decimal MinProductPrice { get; set; } = 0.01m;
    public int MaxProductNameLength { get; set; } = 100;
    public int MaxDescriptionLength { get; set; } = 500;
    public int MaxModifierOptions { get; set; } = 50;
    public bool RequireProductImages { get; set; } = false;
    public bool ValidateImageUrls { get; set; } = true;
    public List<string> RequiredFields { get; set; } = new() { "Name", "Price" };
    public Dictionary<string, object> CustomRules { get; set; } = new();
}