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
/// Adapter that provides backward compatibility for existing sync operations
/// Automatically handles Menu Group resolution and maintains legacy API compatibility
/// </summary>
public class MenuGroupSyncAdapter : IMenuGroupSyncAdapter, ITransientDependency
{
    private readonly MenuGroupCompatibilityService _compatibilityService;
    private readonly MenuSyncOrchestrator _syncOrchestrator;
    private readonly ILogger<MenuGroupSyncAdapter> _logger;

    public MenuGroupSyncAdapter(
        MenuGroupCompatibilityService compatibilityService,
        MenuSyncOrchestrator syncOrchestrator,
        ILogger<MenuGroupSyncAdapter> logger)
    {
        _compatibilityService = compatibilityService;
        _syncOrchestrator = syncOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Legacy sync method that automatically handles Menu Group resolution
    /// Maintains backward compatibility while enabling Menu Group features
    /// </summary>
    public async Task<MenuSyncRun> ExecuteLegacySyncAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        string talabatVendorCode,
        string syncType = MenuSyncRunType.Manual,
        string triggerSource = MenuSyncTriggerSource.User,
        string initiatedBy = "System",
        bool forceFullSync = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting legacy sync with Menu Group compatibility. Account={AccountId}, Branch={BranchId}, Products={Count}",
            foodicsAccountId, branchId ?? "ALL", currentProducts.Count);

        try
        {
            // Step 1: Resolve Menu Group for this sync operation
            var resolution = await _compatibilityService.ResolveMenuGroupForSyncAsync(
                foodicsAccountId, branchId, null, currentProducts, enableAutoCreation: true, cancellationToken);

            if (!resolution.Success)
            {
                _logger.LogError(
                    "Failed to resolve Menu Group for legacy sync: {Error}",
                    resolution.ErrorMessage);
                throw new InvalidOperationException($"Menu Group resolution failed: {resolution.ErrorMessage}");
            }

            // Step 2: Log the sync mode being used
            LogSyncMode(resolution, foodicsAccountId, branchId);

            // Step 3: Execute sync with resolved Menu Group (or null for legacy mode)
            var syncRun = await _syncOrchestrator.ExecuteFullSyncAsync(
                foodicsAccountId,
                branchId,
                currentProducts,
                talabatVendorCode,
                resolution.MenuGroupId, // This will be null for legacy branch-level sync
                syncType,
                triggerSource,
                initiatedBy,
                forceFullSync,
                cancellationToken);

            // Step 4: Add compatibility metadata to sync run
            await AddCompatibilityMetadataAsync(syncRun, resolution, cancellationToken);

            _logger.LogInformation(
                "Legacy sync completed successfully. SyncRunId={SyncRunId}, Mode={Mode}, MenuGroupId={MenuGroupId}",
                syncRun.Id, resolution.SyncMode, resolution.MenuGroupId?.ToString() ?? "NULL");

            return syncRun;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Legacy sync failed. Account={AccountId}, Branch={BranchId}",
                foodicsAccountId, branchId ?? "ALL");
            throw;
        }
    }

    /// <summary>
    /// Enhanced sync method that accepts optional Menu Group ID
    /// Provides smooth transition path for clients adopting Menu Group features
    /// </summary>
    public async Task<MenuSyncRun> ExecuteEnhancedSyncAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        string talabatVendorCode,
        Guid? menuGroupId = null,
        string syncType = MenuSyncRunType.Manual,
        string triggerSource = MenuSyncTriggerSource.User,
        string initiatedBy = "System",
        bool forceFullSync = false,
        bool enableAutoCreation = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting enhanced sync. Account={AccountId}, Branch={BranchId}, MenuGroupId={MenuGroupId}, Products={Count}",
            foodicsAccountId, branchId ?? "ALL", menuGroupId?.ToString() ?? "NULL", currentProducts.Count);

        try
        {
            // Step 1: Resolve Menu Group for this sync operation
            var resolution = await _compatibilityService.ResolveMenuGroupForSyncAsync(
                foodicsAccountId, branchId, menuGroupId, currentProducts, enableAutoCreation, cancellationToken);

            if (!resolution.Success)
            {
                _logger.LogError(
                    "Failed to resolve Menu Group for enhanced sync: {Error}",
                    resolution.ErrorMessage);
                throw new InvalidOperationException($"Menu Group resolution failed: {resolution.ErrorMessage}");
            }

            // Step 2: Log the sync mode being used
            LogSyncMode(resolution, foodicsAccountId, branchId);

            // Step 3: Execute sync with resolved Menu Group
            var syncRun = await _syncOrchestrator.ExecuteFullSyncAsync(
                foodicsAccountId,
                branchId,
                currentProducts,
                talabatVendorCode,
                resolution.MenuGroupId,
                syncType,
                triggerSource,
                initiatedBy,
                forceFullSync,
                cancellationToken);

            // Step 4: Add compatibility metadata to sync run
            await AddCompatibilityMetadataAsync(syncRun, resolution, cancellationToken);

            _logger.LogInformation(
                "Enhanced sync completed successfully. SyncRunId={SyncRunId}, Mode={Mode}, MenuGroupId={MenuGroupId}",
                syncRun.Id, resolution.SyncMode, resolution.MenuGroupId?.ToString() ?? "NULL");

            return syncRun;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Enhanced sync failed. Account={AccountId}, Branch={BranchId}, MenuGroupId={MenuGroupId}",
                foodicsAccountId, branchId ?? "ALL", menuGroupId?.ToString() ?? "NULL");
            throw;
        }
    }

    /// <summary>
    /// Validates that a sync operation will be backward compatible
    /// </summary>
    public async Task<SyncCompatibilityCheck> CheckSyncCompatibilityAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new SyncCompatibilityCheck
            {
                FoodicsAccountId = foodicsAccountId,
                BranchId = branchId,
                RequestedMenuGroupId = menuGroupId,
                CheckedAt = DateTime.UtcNow
            };

            // Check backward compatibility
            var compatibilityResult = await _compatibilityService.ValidateBackwardCompatibilityAsync(
                foodicsAccountId, branchId, cancellationToken);

            result.IsBackwardCompatible = compatibilityResult.IsCompatible;
            result.CompatibilityErrors.AddRange(compatibilityResult.Errors);
            result.CompatibilityWarnings.AddRange(compatibilityResult.Warnings);

            // Resolve Menu Group to see what would happen
            var resolution = await _compatibilityService.ResolveMenuGroupForSyncAsync(
                foodicsAccountId, branchId, menuGroupId, new List<FoodicsProductDetailDto>(), 
                enableAutoCreation: false, cancellationToken);

            result.ResolvedSyncMode = resolution.SyncMode;
            result.ResolvedMenuGroupId = resolution.MenuGroupId;
            result.WouldCreateDefaultMenuGroup = resolution.WasAutoCreated;

            if (!resolution.Success)
            {
                result.ResolutionErrors.Add(resolution.ErrorMessage ?? "Unknown resolution error");
            }

            result.AvailableMenuGroups.AddRange(resolution.AvailableMenuGroups);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check sync compatibility");
            
            return new SyncCompatibilityCheck
            {
                FoodicsAccountId = foodicsAccountId,
                BranchId = branchId,
                RequestedMenuGroupId = menuGroupId,
                CheckedAt = DateTime.UtcNow,
                IsBackwardCompatible = false,
                CompatibilityErrors = { ex.Message }
            };
        }
    }

    /// <summary>
    /// Gets sync recommendations for transitioning to Menu Groups
    /// </summary>
    public async Task<MenuGroupTransitionRecommendation> GetTransitionRecommendationAsync(
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recommendation = new MenuGroupTransitionRecommendation
            {
                FoodicsAccountId = foodicsAccountId,
                BranchId = branchId,
                GeneratedAt = DateTime.UtcNow
            };

            // Check current state
            var compatibilityCheck = await CheckSyncCompatibilityAsync(
                foodicsAccountId, branchId, null, cancellationToken);

            recommendation.CurrentSyncMode = compatibilityCheck.ResolvedSyncMode;
            recommendation.HasMenuGroups = compatibilityCheck.AvailableMenuGroups.Any();

            // Generate recommendations based on current state
            if (!recommendation.HasMenuGroups)
            {
                recommendation.RecommendedAction = MenuGroupTransitionAction.CreateDefaultMenuGroup;
                recommendation.Recommendations.Add("Create a default Menu Group containing all categories");
                recommendation.Recommendations.Add("This will enable Menu Group features while maintaining backward compatibility");
            }
            else if (compatibilityCheck.AvailableMenuGroups.Count == 1)
            {
                var singleGroup = compatibilityCheck.AvailableMenuGroups[0];
                if (singleGroup.Name == "All Categories")
                {
                    recommendation.RecommendedAction = MenuGroupTransitionAction.OptimizeMenuGroups;
                    recommendation.Recommendations.Add("Consider organizing menu into multiple Menu Groups by brand or category type");
                    recommendation.Recommendations.Add("This will enable better menu isolation and multi-brand operations");
                }
                else
                {
                    recommendation.RecommendedAction = MenuGroupTransitionAction.NoActionNeeded;
                    recommendation.Recommendations.Add("Menu Group setup looks good");
                }
            }
            else
            {
                recommendation.RecommendedAction = MenuGroupTransitionAction.ReviewMenuGroups;
                recommendation.Recommendations.Add("Multiple Menu Groups exist - ensure they are properly configured");
                recommendation.Recommendations.Add("Consider using Menu Group-specific sync calls for better control");
            }

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate transition recommendation");
            
            return new MenuGroupTransitionRecommendation
            {
                FoodicsAccountId = foodicsAccountId,
                BranchId = branchId,
                GeneratedAt = DateTime.UtcNow,
                RecommendedAction = MenuGroupTransitionAction.ContactSupport,
                Recommendations = { $"Error generating recommendations: {ex.Message}" }
            };
        }
    }

    #region Private Methods

    private void LogSyncMode(MenuGroupResolutionResult resolution, Guid foodicsAccountId, string? branchId)
    {
        switch (resolution.SyncMode)
        {
            case MenuGroupSyncMode.LegacyBranchLevel:
                _logger.LogInformation(
                    "Using legacy branch-level sync (no Menu Groups). Account={AccountId}, Branch={BranchId}",
                    foodicsAccountId, branchId ?? "ALL");
                break;

            case MenuGroupSyncMode.DefaultMenuGroup:
                _logger.LogInformation(
                    "Using default Menu Group sync. Account={AccountId}, Branch={BranchId}, MenuGroupId={MenuGroupId}, AutoCreated={AutoCreated}",
                    foodicsAccountId, branchId ?? "ALL", resolution.MenuGroupId, resolution.WasAutoCreated);
                break;

            case MenuGroupSyncMode.MenuGroupSpecific:
                _logger.LogInformation(
                    "Using Menu Group-specific sync. Account={AccountId}, Branch={BranchId}, MenuGroupId={MenuGroupId}",
                    foodicsAccountId, branchId ?? "ALL", resolution.MenuGroupId);
                break;
        }
    }

    private async Task AddCompatibilityMetadataAsync(
        MenuSyncRun syncRun,
        MenuGroupResolutionResult resolution,
        CancellationToken cancellationToken)
    {
        // Add metadata about the compatibility mode used
        var compatibilityMetadata = new Dictionary<string, object>
        {
            ["SyncMode"] = resolution.SyncMode.ToString(),
            ["IsDefaultMenuGroup"] = resolution.IsDefaultMenuGroup,
            ["WasAutoCreated"] = resolution.WasAutoCreated,
            ["BackwardCompatibilityEnabled"] = true
        };

        // This would be added to the sync run's metadata
        // Implementation depends on how metadata is stored in MenuSyncRun
    }

    #endregion
}

#region Result Classes

/// <summary>
/// Result of sync compatibility check
/// </summary>
public class SyncCompatibilityCheck
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public Guid? RequestedMenuGroupId { get; set; }
    public DateTime CheckedAt { get; set; }
    public bool IsBackwardCompatible { get; set; }
    public MenuGroupSyncMode ResolvedSyncMode { get; set; }
    public Guid? ResolvedMenuGroupId { get; set; }
    public bool WouldCreateDefaultMenuGroup { get; set; }
    public List<string> CompatibilityErrors { get; set; } = new();
    public List<string> CompatibilityWarnings { get; set; } = new();
    public List<string> ResolutionErrors { get; set; } = new();
    public List<MenuGroupInfo> AvailableMenuGroups { get; set; } = new();
}

/// <summary>
/// Recommendation for transitioning to Menu Groups
/// </summary>
public class MenuGroupTransitionRecommendation
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public MenuGroupSyncMode CurrentSyncMode { get; set; }
    public bool HasMenuGroups { get; set; }
    public MenuGroupTransitionAction RecommendedAction { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Recommended action for Menu Group transition
/// </summary>
public enum MenuGroupTransitionAction
{
    NoActionNeeded,
    CreateDefaultMenuGroup,
    OptimizeMenuGroups,
    ReviewMenuGroups,
    ContactSupport
}

#endregion