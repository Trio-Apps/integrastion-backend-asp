using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Validation;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// High-level orchestrator that coordinates all menu sync operations
/// Integrates MenuSyncRun tracking with existing sync services
/// </summary>
public class MenuSyncOrchestrator : ITransientDependency
{
    private readonly MenuSyncRunManager _syncRunManager;
    private readonly IMenuDeltaSyncService _deltaSyncService;
    private readonly IMenuSoftDeleteService _softDeleteService;
    private readonly MenuVersioningService _versioningService;
    private readonly IMenuValidationService _validationService;
    private readonly MenuGroupCompatibilityService _compatibilityService;
    private readonly ILogger<MenuSyncOrchestrator> _logger;

    public MenuSyncOrchestrator(
        MenuSyncRunManager syncRunManager,
        IMenuDeltaSyncService deltaSyncService,
        IMenuSoftDeleteService softDeleteService,
        MenuVersioningService versioningService,
        IMenuValidationService validationService,
        MenuGroupCompatibilityService compatibilityService,
        ILogger<MenuSyncOrchestrator> logger)
    {
        _syncRunManager = syncRunManager;
        _deltaSyncService = deltaSyncService;
        _softDeleteService = softDeleteService;
        _versioningService = versioningService;
        _validationService = validationService;
        _compatibilityService = compatibilityService;
        _logger = logger;
    }

    /// <summary>
    /// Orchestrates a complete menu synchronization with full tracking and backward compatibility
    /// Automatically handles Menu Group resolution for legacy sync operations
    /// </summary>
    public async Task<MenuSyncRun> ExecuteFullSyncAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        string talabatVendorCode,
        Guid? menuGroupId = null,
        string syncType = MenuSyncRunType.Manual,
        string triggerSource = MenuSyncTriggerSource.User,
        string initiatedBy = "System",
        bool forceFullSync = false,
        CancellationToken cancellationToken = default)
    {
        // BACKWARD COMPATIBILITY: Resolve Menu Group if not specified
        if (menuGroupId == null)
        {
            _logger.LogInformation(
                "No Menu Group specified - resolving for backward compatibility. Account={AccountId}, Branch={BranchId}",
                foodicsAccountId, branchId ?? "ALL");

            var resolution = await _compatibilityService.ResolveMenuGroupForSyncAsync(
                foodicsAccountId, branchId, null, currentProducts, enableAutoCreation: true, cancellationToken);

            if (resolution.Success)
            {
                menuGroupId = resolution.MenuGroupId; // May still be null for legacy branch-level sync
                
                _logger.LogInformation(
                    "Menu Group resolved for backward compatibility. Mode={Mode}, MenuGroupId={MenuGroupId}, AutoCreated={AutoCreated}",
                    resolution.SyncMode, menuGroupId?.ToString() ?? "NULL", resolution.WasAutoCreated);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to resolve Menu Group for backward compatibility: {Error}. Proceeding with legacy sync.",
                    resolution.ErrorMessage);
            }
        }

        // Start sync run with tracking (including Menu Group context)
        var syncRun = await _syncRunManager.StartSyncRunAsync(
            foodicsAccountId,
            branchId,
            menuGroupId,
            syncType,
            triggerSource,
            initiatedBy,
            new Dictionary<string, object>
            {
                ["ProductCount"] = currentProducts.Count,
                ["TalabatVendorCode"] = talabatVendorCode,
                ["ForceFullSync"] = forceFullSync,
                ["MenuGroupId"] = menuGroupId?.ToString() ?? "ALL",
                ["BackwardCompatibilityMode"] = menuGroupId == null ? "LegacyBranchLevel" : "MenuGroupEnabled"
            },
            cancellationToken);

        try
        {
            // Phase 1: Change Detection
            await _syncRunManager.UpdateProgressAsync(
                syncRun.Id, MenuSyncPhase.ChangeDetection, 10, 
                "Detecting menu changes", cancellationToken: cancellationToken);

            var changeDetection = await _versioningService.DetectChangesAsync(
                foodicsAccountId, branchId, currentProducts, menuGroupId, cancellationToken);

            if (!changeDetection.HasChanged && !forceFullSync)
            {
                await _syncRunManager.CompleteSyncRunAsync(
                    syncRun.Id, "NoChanges", 
                    new Dictionary<string, object> { ["ChangeDetected"] = false },
                    cancellationToken);
                return syncRun;
            }

            // Phase 2: Delta Generation
            await _syncRunManager.UpdateProgressAsync(
                syncRun.Id, MenuSyncPhase.DeltaGeneration, 30,
                "Generating delta payload", cancellationToken: cancellationToken);

            var deltaResult = await _deltaSyncService.GenerateDeltaAsync(
                foodicsAccountId, branchId, currentProducts, menuGroupId, forceFullSync, cancellationToken);

            if (!deltaResult.Success)
            {
                await _syncRunManager.FailSyncRunAsync(
                    syncRun.Id, $"Delta generation failed: {deltaResult.ErrorMessage}", 
                    cancellationToken: cancellationToken);
                return syncRun;
            }

            // Phase 3: Menu Validation
            await _syncRunManager.UpdateProgressAsync(
                syncRun.Id, MenuSyncPhase.DataValidation, 40,
                "Validating menu data", cancellationToken: cancellationToken);

            var validationResult = await _validationService.ValidateMenuAsync(
                currentProducts, foodicsAccountId, branchId, failFast: true, cancellationToken);

            if (!validationResult.CanSubmitToTalabat)
            {
                await _syncRunManager.FailSyncRunAsync(
                    syncRun.Id, $"Menu validation failed: {validationResult.GetSummary()}", 
                    cancellationToken: cancellationToken);
                return syncRun;
            }

            // Add validation warnings if any
            if (validationResult.Warnings.Any())
            {
                await _syncRunManager.AddWarningAsync(
                    syncRun.Id, $"Menu validation warnings: {validationResult.Warnings.Count} issues found",
                    cancellationToken: cancellationToken);
            }

            // Phase 4: Soft Delete Processing
            await _syncRunManager.UpdateProgressAsync(
                syncRun.Id, MenuSyncPhase.SoftDeleteProcessing, 60,
                "Processing soft deletes", cancellationToken: cancellationToken);

            var pendingDeletions = await _softDeleteService.GetPendingDeletionsAsync(
                foodicsAccountId, branchId, cancellationToken);

            if (pendingDeletions.Count > 0)
            {
                var deletionSyncResult = await _softDeleteService.SyncDeletionsToTalabatAsync(
                    foodicsAccountId, talabatVendorCode, cancellationToken: cancellationToken);

                if (!deletionSyncResult.Success)
                {
                    await _syncRunManager.AddWarningAsync(
                        syncRun.Id, $"Some deletions failed to sync: {string.Join(", ", deletionSyncResult.Errors)}",
                        cancellationToken: cancellationToken);
                }
            }

            // Phase 5: Talabat Submission
            await _syncRunManager.UpdateProgressAsync(
                syncRun.Id, MenuSyncPhase.TalabatSubmission, 80,
                "Submitting to Talabat", cancellationToken: cancellationToken);

            if (deltaResult.Payload != null)
            {
                var syncResult = await _deltaSyncService.SyncDeltaToTalabatAsync(
                    deltaResult.Payload.Metadata.DeltaId, talabatVendorCode, cancellationToken);

                await _syncRunManager.SetTalabatSyncInfoAsync(
                    syncRun.Id, talabatVendorCode, syncResult.TalabatImportId, 
                    syncResult.Success ? "Completed" : "Failed", cancellationToken);

                if (!syncResult.Success)
                {
                    await _syncRunManager.FailSyncRunAsync(
                        syncRun.Id, $"Talabat sync failed: {syncResult.ErrorMessage}",
                        cancellationToken: cancellationToken);
                    return syncRun;
                }

                // Update statistics
                await _syncRunManager.UpdateStatisticsAsync(
                    syncRun.Id,
                    totalProcessed: syncResult.ProcessedItems,
                    succeeded: syncResult.ProcessedItems - syncResult.FailedItems,
                    failed: syncResult.FailedItems,
                    cancellationToken: cancellationToken);
            }

            // Phase 6: Finalization
            await _syncRunManager.UpdateProgressAsync(
                syncRun.Id, MenuSyncPhase.Finalization, 95,
                "Finalizing sync", cancellationToken: cancellationToken);

            // Create final metrics
            var finalMetrics = new Dictionary<string, object>
            {
                ["ChangeDetected"] = changeDetection.HasChanged,
                ["DeltaGenerated"] = deltaResult.Success,
                ["DeltaGenerationTime"] = deltaResult.GenerationTime.TotalMilliseconds,
                ["PendingDeletions"] = pendingDeletions.Count,
                ["TotalProducts"] = currentProducts.Count,
                ["ValidationPassed"] = validationResult.IsValid,
                ["ValidationErrors"] = validationResult.Errors.Count,
                ["ValidationWarnings"] = validationResult.Warnings.Count,
                ["ValidationTime"] = validationResult.ValidationDuration.TotalMilliseconds
            };

            await _syncRunManager.CompleteSyncRunAsync(
                syncRun.Id, "Success", finalMetrics, cancellationToken);

            return syncRun;
        }
        catch (Exception ex)
        {
            await _syncRunManager.FailSyncRunAsync(
                syncRun.Id, "Unexpected error during sync", ex, cancellationToken: cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Orchestrates a retry of a failed sync run
    /// </summary>
    public async Task<MenuSyncRun> RetryFailedSyncAsync(
        Guid failedSyncRunId,
        string initiatedBy = "System",
        CancellationToken cancellationToken = default)
    {
        var failedSyncRun = await _syncRunManager.SyncRunRepository.GetAsync(failedSyncRunId, cancellationToken: cancellationToken);

        if (!failedSyncRun.CanRetry)
        {
            throw new InvalidOperationException("Sync run cannot be retried");
        }

        // Create retry sync run
        var retrySyncRun = await _syncRunManager.StartSyncRunAsync(
            failedSyncRun.FoodicsAccountId,
            failedSyncRun.BranchId,
            failedSyncRun.MenuGroupId,
            MenuSyncRunType.Retry,
            MenuSyncTriggerSource.RetryJob,
            initiatedBy,
            new Dictionary<string, object>
            {
                ["ParentSyncRunId"] = failedSyncRunId,
                ["RetryAttempt"] = failedSyncRun.RetryCount + 1
            },
            cancellationToken);

        // Link to parent
        retrySyncRun.ParentSyncRunId = failedSyncRunId;
        retrySyncRun.RetryCount = failedSyncRun.RetryCount + 1;

        // Update failed sync run
        failedSyncRun.RetryCount++;
        await _syncRunManager.SyncRunRepository.UpdateAsync(failedSyncRun, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Started retry sync run. OriginalSyncRunId={OriginalSyncRunId}, RetrySyncRunId={RetrySyncRunId}, RetryCount={RetryCount}",
            failedSyncRunId, retrySyncRun.Id, retrySyncRun.RetryCount);

        return retrySyncRun;
    }

    /// <summary>
    /// Gets comprehensive sync status for monitoring
    /// </summary>
    public async Task<SyncStatusReport> GetSyncStatusAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var activeSyncRuns = await _syncRunManager.GetActiveSyncRunsAsync(foodicsAccountId, branchId, cancellationToken);
        var latestSyncRun = await _syncRunManager.SyncRunRepository.GetLatestSyncRunAsync(foodicsAccountId, branchId, cancellationToken);
        var pendingDeletions = await _softDeleteService.GetPendingDeletionsAsync(foodicsAccountId, branchId, cancellationToken);

        var report = new SyncStatusReport
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            HasActiveSyncRuns = activeSyncRuns.Count > 0,
            ActiveSyncRunsCount = activeSyncRuns.Count,
            LatestSyncRun = latestSyncRun,
            PendingDeletionsCount = pendingDeletions.Count,
            LastSyncStatus = latestSyncRun?.Status,
            LastSyncResult = latestSyncRun?.Result,
            LastSyncDuration = latestSyncRun?.Duration,
            LastSyncProductsProcessed = latestSyncRun?.TotalProductsProcessed ?? 0,
            LastSyncSuccessRate = latestSyncRun?.SuccessRate ?? 0
        };

        return report;
    }
}

/// <summary>
/// Comprehensive sync status report
/// </summary>
public class SyncStatusReport
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public bool HasActiveSyncRuns { get; set; }
    public int ActiveSyncRunsCount { get; set; }
    public MenuSyncRun? LatestSyncRun { get; set; }
    public int PendingDeletionsCount { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastSyncResult { get; set; }
    public TimeSpan? LastSyncDuration { get; set; }
    public int LastSyncProductsProcessed { get; set; }
    public double LastSyncSuccessRate { get; set; }
}