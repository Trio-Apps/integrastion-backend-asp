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
/// Integration service that coordinates all backward compatibility components
/// Provides a unified interface for testing and validating backward compatibility
/// </summary>
public class MenuGroupBackwardCompatibilityIntegrationService : ITransientDependency
{
    private readonly IMenuGroupCompatibilityService _compatibilityService;
    private readonly IMenuGroupSyncAdapter _syncAdapter;
    private readonly IMenuGroupRollbackService _rollbackService;
    private readonly MenuSyncOrchestrator _syncOrchestrator;
    private readonly ILogger<MenuGroupBackwardCompatibilityIntegrationService> _logger;

    public MenuGroupBackwardCompatibilityIntegrationService(
        IMenuGroupCompatibilityService compatibilityService,
        IMenuGroupSyncAdapter syncAdapter,
        IMenuGroupRollbackService rollbackService,
        MenuSyncOrchestrator syncOrchestrator,
        ILogger<MenuGroupBackwardCompatibilityIntegrationService> logger)
    {
        _compatibilityService = compatibilityService;
        _syncAdapter = syncAdapter;
        _rollbackService = rollbackService;
        _syncOrchestrator = syncOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Performs comprehensive backward compatibility validation
    /// Tests all compatibility scenarios and reports results
    /// </summary>
    public async Task<BackwardCompatibilityTestResult> ValidateBackwardCompatibilityAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> testProducts,
        string talabatVendorCode,
        CancellationToken cancellationToken = default)
    {
        var result = new BackwardCompatibilityTestResult
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation(
                "Starting backward compatibility validation. Account={AccountId}, Branch={BranchId}",
                foodicsAccountId, branchId ?? "ALL");

            // Test 1: Legacy sync without Menu Groups
            await TestLegacySyncWithoutMenuGroupsAsync(result, foodicsAccountId, branchId, testProducts, talabatVendorCode, cancellationToken);

            // Test 2: Legacy sync with auto-created Menu Group
            await TestLegacySyncWithAutoCreatedMenuGroupAsync(result, foodicsAccountId, branchId, testProducts, talabatVendorCode, cancellationToken);

            // Test 3: Enhanced sync with explicit Menu Group
            await TestEnhancedSyncWithExplicitMenuGroupAsync(result, foodicsAccountId, branchId, testProducts, talabatVendorCode, cancellationToken);

            // Test 4: Compatibility validation
            await TestCompatibilityValidationAsync(result, foodicsAccountId, branchId, cancellationToken);

            // Test 5: Transition recommendations
            await TestTransitionRecommendationsAsync(result, foodicsAccountId, branchId, cancellationToken);

            // Test 6: Rollback capabilities
            await TestRollbackCapabilitiesAsync(result, foodicsAccountId, branchId, cancellationToken);

            result.OverallSuccess = result.TestResults.All(t => t.Success);
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            _logger.LogInformation(
                "Backward compatibility validation completed. Success={Success}, Tests={TestCount}, Duration={Duration}ms",
                result.OverallSuccess, result.TestResults.Count, result.Duration?.TotalMilliseconds ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backward compatibility validation failed");
            
            result.OverallSuccess = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;
            
            return result;
        }
    }

    /// <summary>
    /// Demonstrates the migration path from legacy to Menu Group-enabled sync
    /// </summary>
    public async Task<MigrationDemonstrationResult> DemonstrateMigrationPathAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> testProducts,
        string talabatVendorCode,
        CancellationToken cancellationToken = default)
    {
        var result = new MigrationDemonstrationResult
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation(
                "Starting migration path demonstration. Account={AccountId}, Branch={BranchId}",
                foodicsAccountId, branchId ?? "ALL");

            // Phase 1: Show current state (legacy)
            var initialState = await _syncAdapter.CheckSyncCompatibilityAsync(foodicsAccountId, branchId, null, cancellationToken);
            result.Phases.Add(new MigrationDemonstrationPhase
            {
                Name = "Initial State",
                Description = $"Current sync mode: {initialState.ResolvedSyncMode}",
                Success = true,
                Details = new Dictionary<string, object>
                {
                    ["SyncMode"] = initialState.ResolvedSyncMode.ToString(),
                    ["HasMenuGroups"] = initialState.AvailableMenuGroups.Any(),
                    ["MenuGroupCount"] = initialState.AvailableMenuGroups.Count
                }
            });

            // Phase 2: Execute legacy sync (should work unchanged)
            var legacySyncRun = await _syncAdapter.ExecuteLegacySyncAsync(
                foodicsAccountId, branchId, testProducts, talabatVendorCode, 
                cancellationToken: cancellationToken);

            result.Phases.Add(new MigrationDemonstrationPhase
            {
                Name = "Legacy Sync Execution",
                Description = $"Legacy sync completed with status: {legacySyncRun.Status}",
                Success = legacySyncRun.Status == MenuSyncRunStatus.Completed,
                Details = new Dictionary<string, object>
                {
                    ["SyncRunId"] = legacySyncRun.Id,
                    ["Status"] = legacySyncRun.Status,
                    ["MenuGroupId"] = legacySyncRun.MenuGroupId?.ToString() ?? "NULL",
                    ["Duration"] = legacySyncRun.Duration?.TotalMilliseconds ?? 0
                }
            });

            // Phase 3: Get transition recommendations
            var recommendations = await _syncAdapter.GetTransitionRecommendationAsync(foodicsAccountId, branchId, cancellationToken);
            result.Phases.Add(new MigrationDemonstrationPhase
            {
                Name = "Transition Analysis",
                Description = $"Recommended action: {recommendations.RecommendedAction}",
                Success = true,
                Details = new Dictionary<string, object>
                {
                    ["RecommendedAction"] = recommendations.RecommendedAction.ToString(),
                    ["HasMenuGroups"] = recommendations.HasMenuGroups,
                    ["CurrentSyncMode"] = recommendations.CurrentSyncMode.ToString(),
                    ["Recommendations"] = recommendations.Recommendations
                }
            });

            // Phase 4: Execute enhanced sync (should auto-create Menu Group if needed)
            var enhancedSyncRun = await _syncAdapter.ExecuteEnhancedSyncAsync(
                foodicsAccountId, branchId, testProducts, talabatVendorCode,
                cancellationToken: cancellationToken);

            result.Phases.Add(new MigrationDemonstrationPhase
            {
                Name = "Enhanced Sync Execution",
                Description = $"Enhanced sync completed with status: {enhancedSyncRun.Status}",
                Success = enhancedSyncRun.Status == MenuSyncRunStatus.Completed,
                Details = new Dictionary<string, object>
                {
                    ["SyncRunId"] = enhancedSyncRun.Id,
                    ["Status"] = enhancedSyncRun.Status,
                    ["MenuGroupId"] = enhancedSyncRun.MenuGroupId?.ToString() ?? "NULL",
                    ["Duration"] = enhancedSyncRun.Duration?.TotalMilliseconds ?? 0
                }
            });

            // Phase 5: Show final state
            var finalState = await _syncAdapter.CheckSyncCompatibilityAsync(foodicsAccountId, branchId, null, cancellationToken);
            result.Phases.Add(new MigrationDemonstrationPhase
            {
                Name = "Final State",
                Description = $"Final sync mode: {finalState.ResolvedSyncMode}",
                Success = true,
                Details = new Dictionary<string, object>
                {
                    ["SyncMode"] = finalState.ResolvedSyncMode.ToString(),
                    ["HasMenuGroups"] = finalState.AvailableMenuGroups.Any(),
                    ["MenuGroupCount"] = finalState.AvailableMenuGroups.Count,
                    ["WouldCreateDefault"] = finalState.WouldCreateDefaultMenuGroup
                }
            });

            result.Success = result.Phases.All(p => p.Success);
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration path demonstration failed");
            
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;
            
            return result;
        }
    }

    #region Private Test Methods

    private async Task TestLegacySyncWithoutMenuGroupsAsync(
        BackwardCompatibilityTestResult result,
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> testProducts,
        string talabatVendorCode,
        CancellationToken cancellationToken)
    {
        var testResult = new CompatibilityTestResult
        {
            TestName = "Legacy Sync Without Menu Groups",
            Description = "Tests that legacy sync works when no Menu Groups exist"
        };

        try
        {
            // This should work with legacy branch-level sync
            var syncRun = await _syncAdapter.ExecuteLegacySyncAsync(
                foodicsAccountId, branchId, testProducts, talabatVendorCode, cancellationToken: cancellationToken);

            testResult.Success = syncRun != null;
            testResult.Details["SyncRunId"] = syncRun?.Id.ToString() ?? "NULL";
            testResult.Details["MenuGroupId"] = syncRun?.MenuGroupId?.ToString() ?? "NULL";
            testResult.Details["Status"] = syncRun?.Status ?? "UNKNOWN";
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        result.TestResults.Add(testResult);
    }

    private async Task TestLegacySyncWithAutoCreatedMenuGroupAsync(
        BackwardCompatibilityTestResult result,
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> testProducts,
        string talabatVendorCode,
        CancellationToken cancellationToken)
    {
        var testResult = new CompatibilityTestResult
        {
            TestName = "Legacy Sync With Auto-Created Menu Group",
            Description = "Tests that legacy sync can auto-create Menu Groups when needed"
        };

        try
        {
            // Ensure a default Menu Group exists
            var menuGroupId = await _compatibilityService.EnsureDefaultMenuGroupAsync(
                foodicsAccountId, branchId, testProducts, cancellationToken);

            var syncRun = await _syncAdapter.ExecuteLegacySyncAsync(
                foodicsAccountId, branchId, testProducts, talabatVendorCode, cancellationToken: cancellationToken);

            testResult.Success = syncRun != null;
            testResult.Details["AutoCreatedMenuGroupId"] = menuGroupId?.ToString() ?? "NULL";
            testResult.Details["SyncRunId"] = syncRun?.Id.ToString() ?? "NULL";
            testResult.Details["UsedMenuGroupId"] = syncRun?.MenuGroupId?.ToString() ?? "NULL";
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        result.TestResults.Add(testResult);
    }

    private async Task TestEnhancedSyncWithExplicitMenuGroupAsync(
        BackwardCompatibilityTestResult result,
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> testProducts,
        string talabatVendorCode,
        CancellationToken cancellationToken)
    {
        var testResult = new CompatibilityTestResult
        {
            TestName = "Enhanced Sync With Explicit Menu Group",
            Description = "Tests enhanced sync with explicit Menu Group specification"
        };

        try
        {
            // First ensure we have a Menu Group
            var menuGroupId = await _compatibilityService.EnsureDefaultMenuGroupAsync(
                foodicsAccountId, branchId, testProducts, cancellationToken);

            if (menuGroupId.HasValue)
            {
                var syncRun = await _syncAdapter.ExecuteEnhancedSyncAsync(
                    foodicsAccountId, branchId, testProducts, talabatVendorCode, 
                    menuGroupId: menuGroupId.Value, cancellationToken: cancellationToken);

                testResult.Success = syncRun != null && syncRun.MenuGroupId == menuGroupId.Value;
                testResult.Details["RequestedMenuGroupId"] = menuGroupId.Value.ToString();
                testResult.Details["UsedMenuGroupId"] = syncRun?.MenuGroupId?.ToString() ?? "NULL";
                testResult.Details["SyncRunId"] = syncRun?.Id.ToString() ?? "NULL";
            }
            else
            {
                testResult.Success = false;
                testResult.ErrorMessage = "Could not create Menu Group for test";
            }
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        result.TestResults.Add(testResult);
    }

    private async Task TestCompatibilityValidationAsync(
        BackwardCompatibilityTestResult result,
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken)
    {
        var testResult = new CompatibilityTestResult
        {
            TestName = "Compatibility Validation",
            Description = "Tests backward compatibility validation"
        };

        try
        {
            var validation = await _compatibilityService.ValidateBackwardCompatibilityAsync(
                foodicsAccountId, branchId, cancellationToken);

            testResult.Success = validation.IsCompatible;
            testResult.Details["IsCompatible"] = validation.IsCompatible;
            testResult.Details["ErrorCount"] = validation.Errors.Count;
            testResult.Details["WarningCount"] = validation.Warnings.Count;
            
            if (!validation.IsCompatible)
            {
                testResult.ErrorMessage = string.Join("; ", validation.Errors);
            }
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        result.TestResults.Add(testResult);
    }

    private async Task TestTransitionRecommendationsAsync(
        BackwardCompatibilityTestResult result,
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken)
    {
        var testResult = new CompatibilityTestResult
        {
            TestName = "Transition Recommendations",
            Description = "Tests transition recommendation generation"
        };

        try
        {
            var recommendations = await _syncAdapter.GetTransitionRecommendationAsync(
                foodicsAccountId, branchId, cancellationToken);

            testResult.Success = recommendations != null;
            testResult.Details["RecommendedAction"] = recommendations?.RecommendedAction.ToString() ?? "NULL";
            testResult.Details["HasMenuGroups"] = recommendations?.HasMenuGroups ?? false;
            testResult.Details["RecommendationCount"] = recommendations?.Recommendations.Count ?? 0;
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        result.TestResults.Add(testResult);
    }

    private async Task TestRollbackCapabilitiesAsync(
        BackwardCompatibilityTestResult result,
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken)
    {
        var testResult = new CompatibilityTestResult
        {
            TestName = "Rollback Capabilities",
            Description = "Tests rollback validation and planning"
        };

        try
        {
            var options = new MenuGroupRollbackOptions
            {
                RollbackScope = MenuGroupRollbackScope.SpecificAccount,
                TargetFoodicsAccountId = foodicsAccountId,
                TargetBranchId = branchId,
                DryRun = true,
                PreserveData = true
            };

            var validation = await _rollbackService.ValidateRollbackAsync(options, cancellationToken);
            var impact = await _rollbackService.AnalyzeRollbackImpactAsync(options, cancellationToken);
            var plan = await _rollbackService.CreateRollbackPlanAsync(options, cancellationToken);

            testResult.Success = validation.CanRollback && impact != null && plan != null;
            testResult.Details["CanRollback"] = validation.CanRollback;
            testResult.Details["AffectedMenuGroups"] = impact?.AffectedMenuGroups ?? 0;
            testResult.Details["PlanStepCount"] = plan?.Steps.Count ?? 0;
            testResult.Details["EstimatedDuration"] = plan?.EstimatedDuration.TotalMinutes ?? 0;
        }
        catch (Exception ex)
        {
            testResult.Success = false;
            testResult.ErrorMessage = ex.Message;
        }

        result.TestResults.Add(testResult);
    }

    #endregion
}

#region Result Classes

/// <summary>
/// Result of comprehensive backward compatibility testing
/// </summary>
public class BackwardCompatibilityTestResult
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool OverallSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CompatibilityTestResult> TestResults { get; set; } = new();
}

/// <summary>
/// Result of individual compatibility test
/// </summary>
public class CompatibilityTestResult
{
    public string TestName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Result of migration path demonstration
/// </summary>
public class MigrationDemonstrationResult
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<MigrationDemonstrationPhase> Phases { get; set; } = new();
}

/// <summary>
/// Individual phase in migration demonstration
/// </summary>
public class MigrationDemonstrationPhase
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Success { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

#endregion