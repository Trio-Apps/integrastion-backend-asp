using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Versioning.DTOs;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Validation;

/// <summary>
/// Main validation service implementation for menu validation pipeline
/// Coordinates all validation layers with fail-fast strategy
/// </summary>
public class MenuValidationService : IMenuValidationService, ITransientDependency
{
    private readonly IRequiredFieldsValidator _requiredFieldsValidator;
    private readonly IPriceConsistencyValidator _priceConsistencyValidator;
    private readonly IModifierCorrectnessValidator _modifierCorrectnessValidator;
    private readonly ILogger<MenuValidationService> _logger;

    public MenuValidationService(
        IRequiredFieldsValidator requiredFieldsValidator,
        IPriceConsistencyValidator priceConsistencyValidator,
        IModifierCorrectnessValidator modifierCorrectnessValidator,
        ILogger<MenuValidationService> logger)
    {
        _requiredFieldsValidator = requiredFieldsValidator;
        _priceConsistencyValidator = priceConsistencyValidator;
        _modifierCorrectnessValidator = modifierCorrectnessValidator;
        _logger = logger;
    }

    public async Task<MenuValidationResult> ValidateMenuAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId = null,
        bool failFast = true,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var productsList = products.ToList();

        _logger.LogInformation(
            "Starting menu validation. AccountId={AccountId}, BranchId={BranchId}, ProductCount={Count}, FailFast={FailFast}",
            foodicsAccountId, branchId ?? "ALL", productsList.Count, failFast);

        var result = new MenuValidationResult
        {
            ValidatedAt = DateTime.UtcNow
        };

        try
        {
            // Get validation configuration
            var config = await GetValidationConfigurationAsync(foodicsAccountId, branchId, cancellationToken);

            // Calculate statistics
            result.Statistics = CalculateStatistics(productsList);

            // Phase 1: Required Fields Validation (Critical)
            _logger.LogDebug("Phase 1: Validating required fields");
            var requiredFieldsResult = await _requiredFieldsValidator.ValidateAsync(productsList, config, cancellationToken);
            result.Errors.AddRange(requiredFieldsResult.Errors);
            result.Warnings.AddRange(requiredFieldsResult.Warnings);

            // Fail-fast on critical errors
            if (failFast && result.CriticalErrors.Any())
            {
                result.IsValid = false;
                stopwatch.Stop();
                result.ValidationDuration = stopwatch.Elapsed;

                _logger.LogWarning(
                    "Menu validation failed fast on required fields. CriticalErrors={Count}, Time={Time}ms",
                    result.CriticalErrors.Count, stopwatch.ElapsedMilliseconds);

                return result;
            }

            // Phase 2: Price Consistency Validation
            _logger.LogDebug("Phase 2: Validating price consistency");
            var priceResult = await _priceConsistencyValidator.ValidateAsync(productsList, config, cancellationToken);
            result.Errors.AddRange(priceResult.Errors);
            result.Warnings.AddRange(priceResult.Warnings);

            // Fail-fast on critical errors
            if (failFast && result.CriticalErrors.Any())
            {
                result.IsValid = false;
                stopwatch.Stop();
                result.ValidationDuration = stopwatch.Elapsed;

                _logger.LogWarning(
                    "Menu validation failed fast on price consistency. CriticalErrors={Count}, Time={Time}ms",
                    result.CriticalErrors.Count, stopwatch.ElapsedMilliseconds);

                return result;
            }

            // Phase 3: Modifier Correctness Validation
            _logger.LogDebug("Phase 3: Validating modifier correctness");
            var modifierResult = await _modifierCorrectnessValidator.ValidateAsync(productsList, config, cancellationToken);
            result.Errors.AddRange(modifierResult.Errors);
            result.Warnings.AddRange(modifierResult.Warnings);

            // Final validation result
            result.IsValid = !result.BlockingErrors.Any();

            stopwatch.Stop();
            result.ValidationDuration = stopwatch.Elapsed;

            // Update statistics with validation results
            result.Statistics.ValidProducts = productsList.Count - result.Errors.Count(e => e.EntityType == "Product");
            result.Statistics.InvalidProducts = result.Errors.Count(e => e.EntityType == "Product");
            result.Statistics.ProductsWithWarnings = result.Warnings.Count(w => w.EntityType == "Product");

            _logger.LogInformation(
                "Menu validation completed. Valid={Valid}, Errors={Errors}, Warnings={Warnings}, Time={Time}ms",
                result.IsValid, result.Errors.Count, result.Warnings.Count, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Menu validation failed with exception");
            
            result.IsValid = false;
            result.Errors.Add(ValidationError.Critical(
                "VALIDATION_EXCEPTION",
                $"Validation failed with exception: {ex.Message}",
                ValidationCategory.DataIntegrity));

            stopwatch.Stop();
            result.ValidationDuration = stopwatch.Elapsed;

            return result;
        }
    }

    public async Task<MenuValidationResult> ValidateDeltaAsync(
        MenuDeltaPayload deltaPayload,
        bool failFast = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting delta validation. DeltaId={DeltaId}, TotalChanges={Changes}, FailFast={FailFast}",
            deltaPayload.Metadata.DeltaId, deltaPayload.Metadata.TotalChanges, failFast);

        // Convert delta items to products for validation
        var productsToValidate = new List<FoodicsProductDetailDto>();

        // Add added products
        foreach (var addedProduct in deltaPayload.AddedProducts)
        {
            productsToValidate.Add(ConvertDeltaItemToProduct(addedProduct, deltaPayload));
        }

        // Add updated products
        foreach (var updatedProduct in deltaPayload.UpdatedProducts)
        {
            productsToValidate.Add(ConvertDeltaItemToProduct(updatedProduct, deltaPayload));
        }

        // Validate the converted products
        return await ValidateProductsAsync(
            productsToValidate,
            new ValidationOptions { FailFast = failFast },
            cancellationToken);
    }

    public async Task<MenuValidationResult> ValidateProductsAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ValidationOptions();
        var productsList = products.ToList();

        _logger.LogInformation(
            "Starting product validation. ProductCount={Count}, FailFast={FailFast}",
            productsList.Count, options.FailFast);

        // Use default configuration for targeted validation
        var config = new ValidationConfiguration
        {
            FoodicsAccountId = Guid.Empty, // Default config
            BranchId = null
        };

        return await ValidateMenuAsync(productsList, config.FoodicsAccountId, config.BranchId, options.FailFast, cancellationToken);
    }

    public async Task<ValidationConfiguration> GetValidationConfigurationAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Load from database or configuration service
        // For now, return default configuration
        
        _logger.LogDebug(
            "Loading validation configuration. AccountId={AccountId}, BranchId={BranchId}",
            foodicsAccountId, branchId ?? "ALL");

        await Task.CompletedTask; // Placeholder for async configuration loading

        return new ValidationConfiguration
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            MaxProductPrice = 1000m,
            MinProductPrice = 0.01m,
            MaxProductNameLength = 100,
            MaxDescriptionLength = 500,
            MaxModifierOptions = 50,
            RequireProductImages = false,
            ValidateImageUrls = true,
            RequiredFields = new List<string> { "Name", "Price" }
        };
    }

    #region Private Methods

    private ValidationStatistics CalculateStatistics(List<FoodicsProductDetailDto> products)
    {
        var stats = new ValidationStatistics
        {
            TotalProducts = products.Count
        };

        // Calculate category statistics
        var categories = products
            .Where(p => p.Category != null)
            .Select(p => p.Category!)
            .DistinctBy(c => c.Id)
            .ToList();
        stats.TotalCategories = categories.Count;

        // Calculate modifier statistics
        var modifiers = products
            .Where(p => p.Modifiers != null)
            .SelectMany(p => p.Modifiers!)
            .DistinctBy(m => m.Id)
            .ToList();
        stats.TotalModifiers = modifiers.Count;

        var modifierOptions = modifiers
            .Where(m => m.Options != null)
            .SelectMany(m => m.Options!)
            .ToList();
        stats.TotalModifierOptions = modifierOptions.Count;

        // Calculate price statistics
        var prices = products
            .Where(p => p.Price.HasValue && p.Price > 0)
            .Select(p => p.Price!.Value)
            .ToList();

        if (prices.Any())
        {
            stats.AverageProductPrice = prices.Average();
            stats.MinProductPrice = prices.Min();
            stats.MaxProductPrice = prices.Max();
        }

        // Calculate image statistics
        stats.ProductsWithImages = products.Count(p => !string.IsNullOrWhiteSpace(p.Image));
        stats.ProductsWithoutImages = products.Count(p => string.IsNullOrWhiteSpace(p.Image));

        // Calculate orphaned products (products without category)
        stats.OrphanedProducts = products.Count(p => p.Category == null && string.IsNullOrWhiteSpace(p.CategoryId));

        // Calculate empty categories (categories with no active products)
        var categoriesWithProducts = products
            .Where(p => p.IsActive == true && (p.Category != null || !string.IsNullOrWhiteSpace(p.CategoryId)))
            .Select(p => p.Category?.Id ?? p.CategoryId!)
            .Distinct()
            .ToHashSet();

        stats.EmptyCategories = categories.Count(c => !categoriesWithProducts.Contains(c.Id));

        // Calculate modifiers without options
        stats.ModifiersWithoutOptions = modifiers.Count(m => m.Options == null || m.Options.Count == 0);

        return stats;
    }

    private FoodicsProductDetailDto ConvertDeltaItemToProduct(ProductDeltaItem deltaItem, MenuDeltaPayload deltaPayload)
    {
        // Convert delta item back to product for validation
        // This is a simplified conversion - in a real scenario, you might need more complete data
        
        var product = new FoodicsProductDetailDto
        {
            Id = deltaItem.Id,
            Name = deltaItem.Name,
            Price = deltaItem.Price,
            IsActive = deltaItem.IsActive,
            Description = deltaItem.Description,
            CategoryId = deltaItem.CategoryId
        };

        // Find category info from delta payload
        if (!string.IsNullOrEmpty(deltaItem.CategoryId))
        {
            var categoryInfo = deltaPayload.Categories.FirstOrDefault(c => c.Id == deltaItem.CategoryId);
            if (categoryInfo != null)
            {
                product.Category = new FoodicsCategoryInfoDto
                {
                    Id = categoryInfo.Id,
                    Name = categoryInfo.Name
                };
            }
        }

        // Find modifier info from delta payload
        if (deltaItem.ModifierIds != null && deltaItem.ModifierIds.Any())
        {
            product.Modifiers = deltaPayload.Modifiers
                .Where(m => deltaItem.ModifierIds.Contains(m.Id))
                .Select(m => new FoodicsModifierDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    MinAllowed = m.MinSelection,
                    MaxAllowed = m.MaxSelection,
                    Options = m.Options?.Select(o => new FoodicsModifierOptionDto
                    {
                        Id = o.Id,
                        Name = o.Name,
                        Price = o.Price,
                        // IsActive doesn't exist - check if option has branches
                        Branches = o.IsActive 
                            ? new List<FoodicsBranchDto> { new FoodicsBranchDto { Id = "default" } } 
                            : new List<FoodicsBranchDto>()
                    }).ToList()
                }).ToList();
        }

        return product;
    }

    #endregion
}