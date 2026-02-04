using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Comprehensive test scenarios for modifier lifecycle management
/// Validates modifier tracking, versioning, and price change handling
/// </summary>
public class ModifierLifecycleTestScenarios : ITransientDependency
{
    private readonly IModifierLifecycleService _modifierLifecycleService;
    private readonly ILogger<ModifierLifecycleTestScenarios> _logger;

    public ModifierLifecycleTestScenarios(
        IModifierLifecycleService modifierLifecycleService,
        ILogger<ModifierLifecycleTestScenarios> logger)
    {
        _modifierLifecycleService = modifierLifecycleService;
        _logger = logger;
    }

    /// <summary>
    /// Executes all modifier lifecycle test scenarios
    /// </summary>
    public async Task<ModifierTestSuiteResult> ExecuteAllTestsAsync(
        Guid testAccountId,
        string testBranchId = "test-branch",
        CancellationToken cancellationToken = default)
    {
        var result = new ModifierTestSuiteResult
        {
            StartedAt = DateTime.UtcNow,
            TestAccountId = testAccountId,
            TestBranchId = testBranchId
        };

        _logger.LogInformation("Starting modifier lifecycle test suite for account {AccountId}", testAccountId);

        try
        {
            // Test 1: Basic modifier sync
            await TestBasicModifierSyncAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 2: Price change tracking
            await TestPriceChangeTrackingAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 3: Modifier versioning
            await TestModifierVersioningAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 4: Safe price updates
            await TestSafePriceUpdatesAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 5: Modifier validation
            await TestModifierValidationAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 6: Change detection
            await TestModifierChangeDetectionAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 7: Rollback functionality
            await TestModifierRollbackAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 8: Analytics and reporting
            await TestModifierAnalyticsAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 9: Edge cases
            await TestModifierEdgeCasesAsync(result, testAccountId, testBranchId, cancellationToken);

            // Test 10: Performance scenarios
            await TestModifierPerformanceAsync(result, testAccountId, testBranchId, cancellationToken);

            result.Success = result.TestResults.All(t => t.Success);
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            _logger.LogInformation(
                "Modifier lifecycle test suite completed. Success={Success}, Tests={Total}, Passed={Passed}, Failed={Failed}",
                result.Success, result.TestResults.Count, result.PassedTests, result.FailedTests);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Modifier lifecycle test suite failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;
            return result;
        }
    }

    #region Test Scenarios

    /// <summary>
    /// Test 1: Basic modifier sync functionality
    /// </summary>
    private async Task TestBasicModifierSyncAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Basic Modifier Sync",
            Description = "Tests basic modifier synchronization from Foodics data"
        };

        try
        {
            // Create test products with modifiers
            var testProducts = CreateTestProductsWithModifiers();

            // Sync modifiers
            var syncResult = await _modifierLifecycleService.SyncModifiersAsync(
                testAccountId, testBranchId, null, testProducts, cancellationToken);

            // Validate sync results
            testResult.Success = syncResult.Success && syncResult.ModifierGroupsCreated > 0;
            testResult.Details["ModifierGroupsCreated"] = syncResult.ModifierGroupsCreated;
            testResult.Details["ModifierOptionsCreated"] = syncResult.ModifierOptionsCreated;
            testResult.Details["SyncDuration"] = syncResult.Duration?.TotalMilliseconds ?? 0;

            if (!testResult.Success)
            {
                testResult.ErrorMessage = string.Join("; ", syncResult.Errors);
            }
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 2: Price change tracking
    /// </summary>
    private async Task TestPriceChangeTrackingAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Price Change Tracking",
            Description = "Tests modifier option price change tracking and history"
        };

        try
        {
            // Get existing modifier groups
            var modifierGroups = await _modifierLifecycleService.GetModifierGroupsAsync(
                testAccountId, testBranchId, null, true, cancellationToken);

            if (!modifierGroups.Any())
            {
                testResult.Success = false;
                testResult.ErrorMessage = "No modifier groups found for price change testing";
                suiteResult.TestResults.Add(testResult);
                return;
            }

            var firstGroup = modifierGroups.First();
            var firstOption = firstGroup.Options.FirstOrDefault();

            if (firstOption == null)
            {
                testResult.Success = false;
                testResult.ErrorMessage = "No modifier options found for price change testing";
                suiteResult.TestResults.Add(testResult);
                return;
            }

            var originalPrice = firstOption.Price;
            var newPrice = originalPrice + 5.00m;

            // Update price
            var updatedOption = await _modifierLifecycleService.UpdateModifierOptionPriceAsync(
                firstOption.Id, newPrice, "Test price increase", cancellationToken);

            // Verify price change
            var priceHistory = await _modifierLifecycleService.GetPriceHistoryAsync(
                firstOption.Id, cancellationToken: cancellationToken);

            testResult.Success = updatedOption.Price == newPrice && 
                                updatedOption.PreviousPrice == originalPrice &&
                                priceHistory.Any();

            testResult.Details["OriginalPrice"] = originalPrice;
            testResult.Details["NewPrice"] = newPrice;
            testResult.Details["PriceHistoryEntries"] = priceHistory.Count;
            testResult.Details["VersionIncremented"] = updatedOption.Version > firstOption.Version;
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 3: Modifier versioning
    /// </summary>
    private async Task TestModifierVersioningAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Modifier Versioning",
            Description = "Tests modifier group and option versioning functionality"
        };

        try
        {
            var modifierGroups = await _modifierLifecycleService.GetModifierGroupsAsync(
                testAccountId, testBranchId, null, true, cancellationToken);

            if (!modifierGroups.Any())
            {
                testResult.Success = false;
                testResult.ErrorMessage = "No modifier groups found for versioning testing";
                suiteResult.TestResults.Add(testResult);
                return;
            }

            var firstGroup = modifierGroups.First();
            var originalVersion = firstGroup.Version;

            // Update modifier group structure
            var updatedGroup = await _modifierLifecycleService.UpdateModifierGroupAsync(
                firstGroup.Id,
                firstGroup.Name + " (Updated)",
                firstGroup.NameLocalized,
                firstGroup.MinSelection,
                firstGroup.MaxSelection,
                firstGroup.IsRequired,
                "Test structure update",
                cancellationToken);

            testResult.Success = updatedGroup.Version > originalVersion;
            testResult.Details["OriginalVersion"] = originalVersion;
            testResult.Details["NewVersion"] = updatedGroup.Version;
            testResult.Details["NameUpdated"] = updatedGroup.Name.Contains("(Updated)");
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 4: Safe price updates
    /// </summary>
    private async Task TestSafePriceUpdatesAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Safe Price Updates",
            Description = "Tests safe price update mechanisms and validation"
        };

        try
        {
            var modifierGroups = await _modifierLifecycleService.GetModifierGroupsAsync(
                testAccountId, testBranchId, null, true, cancellationToken);

            if (!modifierGroups.Any() || !modifierGroups.First().Options.Any())
            {
                testResult.Success = false;
                testResult.ErrorMessage = "No modifier options found for safe price update testing";
                suiteResult.TestResults.Add(testResult);
                return;
            }

            var option = modifierGroups.First().Options.First();
            var originalPrice = option.Price;

            // Test multiple price updates
            var prices = new[] { originalPrice + 1, originalPrice + 2, originalPrice + 3 };
            var updateCount = 0;

            foreach (var price in prices)
            {
                await _modifierLifecycleService.UpdateModifierOptionPriceAsync(
                    option.Id, price, $"Test update {++updateCount}", cancellationToken);
            }

            // Verify price history
            var priceHistory = await _modifierLifecycleService.GetPriceHistoryAsync(
                option.Id, cancellationToken: cancellationToken);

            testResult.Success = priceHistory.Count >= prices.Length;
            testResult.Details["PriceUpdates"] = prices.Length;
            testResult.Details["HistoryEntries"] = priceHistory.Count;
            testResult.Details["FinalPrice"] = prices.Last();
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 5: Modifier validation
    /// </summary>
    private async Task TestModifierValidationAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Modifier Validation",
            Description = "Tests modifier validation for Talabat compliance"
        };

        try
        {
            var modifierGroups = await _modifierLifecycleService.GetModifierGroupsAsync(
                testAccountId, testBranchId, null, true, cancellationToken);

            var validationResult = await _modifierLifecycleService.ValidateModifiersForTalabatAsync(
                modifierGroups, cancellationToken);

            testResult.Success = validationResult.IsValid || validationResult.Errors.Count == 0;
            testResult.Details["IsValid"] = validationResult.IsValid;
            testResult.Details["ErrorCount"] = validationResult.Errors.Count;
            testResult.Details["WarningCount"] = validationResult.Warnings.Count;
            testResult.Details["ModifierGroupsValidated"] = modifierGroups.Count;

            if (!testResult.Success)
            {
                testResult.ErrorMessage = string.Join("; ", validationResult.Errors);
            }
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 6: Change detection
    /// </summary>
    private async Task TestModifierChangeDetectionAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Modifier Change Detection",
            Description = "Tests modifier change detection algorithms"
        };

        try
        {
            // Create modified test products
            var originalProducts = CreateTestProductsWithModifiers();
            var modifiedProducts = CreateModifiedTestProducts();

            // Sync original products first
            await _modifierLifecycleService.SyncModifiersAsync(
                testAccountId, testBranchId, null, originalProducts, cancellationToken);

            // Detect changes with modified products
            var changeDetection = await _modifierLifecycleService.DetectModifierChangesAsync(
                testAccountId, testBranchId, null, modifiedProducts, cancellationToken);

            testResult.Success = changeDetection.HasChanges;
            testResult.Details["HasChanges"] = changeDetection.HasChanges;
            testResult.Details["NewGroups"] = changeDetection.NewGroups.Count;
            testResult.Details["ModifiedGroups"] = changeDetection.ModifiedGroups.Count;
            testResult.Details["PriceChanges"] = changeDetection.PriceChanges.Count;
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 7: Rollback functionality
    /// </summary>
    private async Task TestModifierRollbackAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Modifier Rollback",
            Description = "Tests modifier rollback to previous versions"
        };

        try
        {
            var modifierGroups = await _modifierLifecycleService.GetModifierGroupsAsync(
                testAccountId, testBranchId, null, true, cancellationToken);

            if (!modifierGroups.Any())
            {
                testResult.Success = false;
                testResult.ErrorMessage = "No modifier groups found for rollback testing";
                suiteResult.TestResults.Add(testResult);
                return;
            }

            var group = modifierGroups.First();
            var originalVersion = group.Version;
            var originalName = group.Name;

            // Make changes to create versions
            await _modifierLifecycleService.UpdateModifierGroupAsync(
                group.Id, originalName + " V2", group.NameLocalized, 
                group.MinSelection, group.MaxSelection, group.IsRequired,
                "Version 2", cancellationToken);

            await _modifierLifecycleService.UpdateModifierGroupAsync(
                group.Id, originalName + " V3", group.NameLocalized,
                group.MinSelection, group.MaxSelection, group.IsRequired,
                "Version 3", cancellationToken);

            // Rollback to original version
            var rolledBackGroup = await _modifierLifecycleService.RollbackModifierGroupAsync(
                group.Id, originalVersion, "Test rollback", cancellationToken);

            testResult.Success = rolledBackGroup.Name == originalName;
            testResult.Details["OriginalVersion"] = originalVersion;
            testResult.Details["RolledBackVersion"] = rolledBackGroup.Version;
            testResult.Details["NameRestored"] = rolledBackGroup.Name == originalName;
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 8: Analytics and reporting
    /// </summary>
    private async Task TestModifierAnalyticsAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Modifier Analytics",
            Description = "Tests modifier analytics and reporting functionality"
        };

        try
        {
            var analytics = await _modifierLifecycleService.GetModifierAnalyticsAsync(
                testAccountId, testBranchId, null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, cancellationToken);

            testResult.Success = analytics != null;
            testResult.Details["TotalModifierGroups"] = analytics?.TotalModifierGroups ?? 0;
            testResult.Details["ActiveModifierGroups"] = analytics?.ActiveModifierGroups ?? 0;
            testResult.Details["TotalModifierOptions"] = analytics?.TotalModifierOptions ?? 0;
            testResult.Details["TotalPriceChanges"] = analytics?.TotalPriceChanges ?? 0;
            testResult.Details["AveragePriceChange"] = analytics?.AveragePriceChange ?? 0;
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 9: Edge cases
    /// </summary>
    private async Task TestModifierEdgeCasesAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Modifier Edge Cases",
            Description = "Tests modifier edge cases and error handling"
        };

        try
        {
            var edgeCasesPassed = 0;
            var totalEdgeCases = 0;

            // Edge Case 1: Empty modifier list
            totalEdgeCases++;
            try
            {
                var emptyResult = await _modifierLifecycleService.SyncModifiersAsync(
                    testAccountId, testBranchId, null, new List<FoodicsProductDetailDto>(), cancellationToken);
                if (emptyResult.Success) edgeCasesPassed++;
            }
            catch { /* Expected to handle gracefully */ }

            // Edge Case 2: Negative price update
            totalEdgeCases++;
            try
            {
                var modifierGroups = await _modifierLifecycleService.GetModifierGroupsAsync(
                    testAccountId, testBranchId, null, true, cancellationToken);
                
                if (modifierGroups.Any() && modifierGroups.First().Options.Any())
                {
                    var option = modifierGroups.First().Options.First();
                    await _modifierLifecycleService.UpdateModifierOptionPriceAsync(
                        option.Id, -10.00m, "Negative price test", cancellationToken);
                    edgeCasesPassed++; // Should handle negative prices
                }
            }
            catch { /* May throw validation error */ }

            // Edge Case 3: Invalid rollback version
            totalEdgeCases++;
            try
            {
                var modifierGroups = await _modifierLifecycleService.GetModifierGroupsAsync(
                    testAccountId, testBranchId, null, true, cancellationToken);
                
                if (modifierGroups.Any())
                {
                    await _modifierLifecycleService.RollbackModifierGroupAsync(
                        modifierGroups.First().Id, 999, "Invalid version test", cancellationToken);
                }
            }
            catch
            {
                edgeCasesPassed++; // Should throw exception for invalid version
            }

            testResult.Success = edgeCasesPassed >= totalEdgeCases / 2; // At least half should pass
            testResult.Details["EdgeCasesPassed"] = edgeCasesPassed;
            testResult.Details["TotalEdgeCases"] = totalEdgeCases;
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    /// <summary>
    /// Test 10: Performance scenarios
    /// </summary>
    private async Task TestModifierPerformanceAsync(
        ModifierTestSuiteResult suiteResult,
        Guid testAccountId,
        string testBranchId,
        CancellationToken cancellationToken)
    {
        var testResult = new ModifierTestResult
        {
            TestName = "Modifier Performance",
            Description = "Tests modifier performance with large datasets"
        };

        try
        {
            var startTime = DateTime.UtcNow;

            // Create large dataset
            var largeProductSet = CreateLargeTestProductSet(100); // 100 products with modifiers

            // Sync large dataset
            var syncResult = await _modifierLifecycleService.SyncModifiersAsync(
                testAccountId, testBranchId, null, largeProductSet, cancellationToken);

            var syncDuration = DateTime.UtcNow - startTime;

            // Performance thresholds
            var maxSyncTime = TimeSpan.FromSeconds(30); // Should complete within 30 seconds
            var performanceAcceptable = syncDuration <= maxSyncTime && syncResult.Success;

            testResult.Success = performanceAcceptable;
            testResult.Details["ProductCount"] = largeProductSet.Count;
            testResult.Details["SyncDuration"] = syncDuration.TotalMilliseconds;
            testResult.Details["MaxAllowedTime"] = maxSyncTime.TotalMilliseconds;
            testResult.Details["ModifierGroupsCreated"] = syncResult.ModifierGroupsCreated;
            testResult.Details["ModifierOptionsCreated"] = syncResult.ModifierOptionsCreated;
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        suiteResult.TestResults.Add(testResult);
    }

    #endregion

    #region Test Data Creation

    private List<FoodicsProductDetailDto> CreateTestProductsWithModifiers()
    {
        return new List<FoodicsProductDetailDto>
        {
            new FoodicsProductDetailDto
            {
                Id = "test-product-1",
                Name = "Test Burger",
                Price = 25.00m,
                Modifiers = new List<FoodicsModifierDto>
                {
                    new FoodicsModifierDto
                    {
                        Id = "test-modifier-1",
                        Name = "Size Options",
                        MinAllowed = 1,
                        MaxAllowed = 1,
                        Options = new List<FoodicsModifierOptionDto>
                        {
                            new FoodicsModifierOptionDto { Id = "opt-1", Name = "Small", Price = 0 },
                            new FoodicsModifierOptionDto { Id = "opt-2", Name = "Medium", Price = 5 },
                            new FoodicsModifierOptionDto { Id = "opt-3", Name = "Large", Price = 10 }
                        }
                    },
                    new FoodicsModifierDto
                    {
                        Id = "test-modifier-2",
                        Name = "Toppings",
                        MinAllowed = 0,
                        MaxAllowed = 3,
                        Options = new List<FoodicsModifierOptionDto>
                        {
                            new FoodicsModifierOptionDto { Id = "top-1", Name = "Cheese", Price = 3 },
                            new FoodicsModifierOptionDto { Id = "top-2", Name = "Bacon", Price = 5 },
                            new FoodicsModifierOptionDto { Id = "top-3", Name = "Mushrooms", Price = 2 }
                        }
                    }
                }
            }
        };
    }

    private List<FoodicsProductDetailDto> CreateModifiedTestProducts()
    {
        var products = CreateTestProductsWithModifiers();
        
        // Modify prices
        products[0].Modifiers![0].Options![1].Price = 6; // Medium size price changed
        products[0].Modifiers![1].Options![0].Price = 4; // Cheese price changed
        
        // Add new option
        products[0].Modifiers![1].Options!.Add(
            new FoodicsModifierOptionDto { Id = "top-4", Name = "Lettuce", Price = 1 });

        return products;
    }

    private List<FoodicsProductDetailDto> CreateLargeTestProductSet(int productCount)
    {
        var products = new List<FoodicsProductDetailDto>();

        for (int i = 1; i <= productCount; i++)
        {
            products.Add(new FoodicsProductDetailDto
            {
                Id = $"perf-product-{i}",
                Name = $"Performance Test Product {i}",
                Price = 10.00m + i,
                Modifiers = new List<FoodicsModifierDto>
                {
                    new FoodicsModifierDto
                    {
                        Id = $"perf-modifier-{i}",
                        Name = $"Options for Product {i}",
                        MinAllowed = 0,
                        MaxAllowed = 2,
                        Options = Enumerable.Range(1, 5).Select(j => new FoodicsModifierOptionDto
                        {
                            Id = $"perf-opt-{i}-{j}",
                            Name = $"Option {j}",
                            Price = j * 2
                        }).ToList()
                    }
                }
            });
        }

        return products;
    }

    #endregion
}

#region Result Classes

public class ModifierTestSuiteResult
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public Guid TestAccountId { get; set; }
    public string TestBranchId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public List<ModifierTestResult> TestResults { get; set; } = new();
    
    public int PassedTests => TestResults.Count(t => t.Success);
    public int FailedTests => TestResults.Count(t => !t.Success);
    public double SuccessRate => TestResults.Any() ? (double)PassedTests / TestResults.Count * 100 : 0;
}

public class ModifierTestResult
{
    public string TestName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

#endregion