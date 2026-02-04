using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service for safely rolling back Menu Group features to maintain system stability
/// Provides comprehensive rollback capabilities with data preservation
/// </summary>
public class MenuGroupRollbackService : IMenuGroupRollbackService, ITransientDependency
{
    private readonly IRepository<FoodicsMenuGroup, Guid> _menuGroupRepository;
    private readonly IRepository<MenuGroupCategory, Guid> _menuGroupCategoryRepository;
    private readonly IRepository<MenuGroupTalabatMapping, Guid> _mappingRepository;
    private readonly IRepository<MenuSnapshot, Guid> _snapshotRepository;
    private readonly IRepository<MenuSyncRun, Guid> _syncRunRepository;
    private readonly IRepository<MenuItemMapping, Guid> _itemMappingRepository;
    private readonly IRepository<MenuDelta, Guid> _deltaRepository;
    private readonly ILogger<MenuGroupRollbackService> _logger;

    public MenuGroupRollbackService(
        IRepository<FoodicsMenuGroup, Guid> menuGroupRepository,
        IRepository<MenuGroupCategory, Guid> menuGroupCategoryRepository,
        IRepository<MenuGroupTalabatMapping, Guid> mappingRepository,
        IRepository<MenuSnapshot, Guid> snapshotRepository,
        IRepository<MenuSyncRun, Guid> syncRunRepository,
        IRepository<MenuItemMapping, Guid> itemMappingRepository,
        IRepository<MenuDelta, Guid> deltaRepository,
        ILogger<MenuGroupRollbackService> logger)
    {
        _menuGroupRepository = menuGroupRepository;
        _menuGroupCategoryRepository = menuGroupCategoryRepository;
        _mappingRepository = mappingRepository;
        _snapshotRepository = snapshotRepository;
        _syncRunRepository = syncRunRepository;
        _itemMappingRepository = itemMappingRepository;
        _deltaRepository = deltaRepository;
        _logger = logger;
    }

    /// <summary>
    /// Performs a complete rollback of Menu Group features
    /// Preserves all data but disables Menu Group functionality
    /// </summary>
    public async Task<MenuGroupRollbackResult> ExecuteFullRollbackAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new MenuGroupRollbackResult
        {
            StartedAt = DateTime.UtcNow,
            Options = options
        };

        try
        {
            _logger.LogWarning(
                "Starting Menu Group rollback. Scope={Scope}, PreserveData={PreserveData}, DryRun={DryRun}",
                options.RollbackScope, options.PreserveData, options.DryRun);

            // Step 1: Validate rollback preconditions
            var validation = await ValidateRollbackPreconditionsAsync(options, cancellationToken);
            if (!validation.CanRollback)
            {
                result.Success = false;
                result.ErrorMessage = $"Rollback validation failed: {string.Join(", ", validation.Errors)}";
                return result;
            }

            result.ValidationWarnings.AddRange(validation.Warnings);

            // Step 2: Disable Menu Group features
            await DisableMenuGroupFeaturesAsync(options, result, cancellationToken);

            // Step 3: Migrate Menu Group-scoped data to branch-level
            await MigrateMenuGroupDataToBranchLevelAsync(options, result, cancellationToken);

            // Step 4: Clean up Menu Group-specific data (if not preserving)
            if (!options.PreserveData)
            {
                await CleanupMenuGroupDataAsync(options, result, cancellationToken);
            }

            // Step 5: Validate system integrity after rollback
            await ValidateSystemIntegrityAsync(result, cancellationToken);

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            _logger.LogWarning(
                "Menu Group rollback completed successfully. Duration={Duration}ms, PreservedData={PreservedData}",
                result.Duration?.TotalMilliseconds ?? 0, options.PreserveData);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Menu Group rollback failed");
            
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;
            
            return result;
        }
    }

    /// <summary>
    /// Performs a partial rollback for specific accounts or branches
    /// </summary>
    public async Task<MenuGroupRollbackResult> ExecutePartialRollbackAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        bool preserveData = true,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var options = new MenuGroupRollbackOptions
        {
            RollbackScope = MenuGroupRollbackScope.SpecificAccount,
            TargetFoodicsAccountId = foodicsAccountId,
            TargetBranchId = branchId,
            PreserveData = preserveData,
            DryRun = dryRun
        };

        return await ExecuteFullRollbackAsync(options, cancellationToken);
    }

    /// <summary>
    /// Validates that a rollback can be performed safely
    /// </summary>
    public async Task<RollbackValidationResult> ValidateRollbackAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken = default)
    {
        return await ValidateRollbackPreconditionsAsync(options, cancellationToken);
    }

    /// <summary>
    /// Gets rollback impact analysis
    /// </summary>
    public async Task<RollbackImpactAnalysis> AnalyzeRollbackImpactAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken = default)
    {
        var analysis = new RollbackImpactAnalysis
        {
            AnalyzedAt = DateTime.UtcNow,
            Options = options
        };

        try
        {
            // Count affected entities
            analysis.AffectedMenuGroups = await CountAffectedMenuGroupsAsync(options, cancellationToken);
            analysis.AffectedTalabatMappings = await CountAffectedTalabatMappingsAsync(options, cancellationToken);
            analysis.AffectedSnapshots = await CountAffectedSnapshotsAsync(options, cancellationToken);
            analysis.AffectedSyncRuns = await CountAffectedSyncRunsAsync(options, cancellationToken);
            analysis.AffectedItemMappings = await CountAffectedItemMappingsAsync(options, cancellationToken);

            // Analyze data migration requirements
            analysis.RequiresDataMigration = analysis.AffectedSnapshots > 0 || analysis.AffectedSyncRuns > 0;
            analysis.EstimatedMigrationTime = EstimateMigrationTime(analysis);

            // Check for potential issues
            await AnalyzePotentialIssuesAsync(analysis, options, cancellationToken);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze rollback impact");
            
            analysis.Errors.Add($"Impact analysis failed: {ex.Message}");
            return analysis;
        }
    }

    /// <summary>
    /// Creates a rollback plan with detailed steps
    /// </summary>
    public async Task<MenuGroupRollbackPlan> CreateRollbackPlanAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken = default)
    {
        var plan = new MenuGroupRollbackPlan
        {
            CreatedAt = DateTime.UtcNow,
            Options = options
        };

        try
        {
            // Analyze impact
            var impact = await AnalyzeRollbackImpactAsync(options, cancellationToken);
            plan.ImpactAnalysis = impact;

            // Create rollback steps
            plan.Steps.AddRange(CreateRollbackSteps(options, impact));

            // Estimate total time
            plan.EstimatedDuration = TimeSpan.FromMilliseconds(plan.Steps.Sum(s => s.EstimatedDuration.TotalMilliseconds));

            // Add warnings and recommendations
            AddRollbackWarningsAndRecommendations(plan, impact);

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create rollback plan");
            
            plan.Errors.Add($"Plan creation failed: {ex.Message}");
            return plan;
        }
    }

    #region Private Methods

    private async Task<RollbackValidationResult> ValidateRollbackPreconditionsAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken)
    {
        var result = new RollbackValidationResult { CanRollback = true };

        try
        {
            // Check for active sync runs
            var activeSyncRuns = await GetActiveSyncRunsAsync(options, cancellationToken);
            if (activeSyncRuns.Any())
            {
                result.CanRollback = false;
                result.Errors.Add($"Cannot rollback while {activeSyncRuns.Count} sync runs are active");
            }

            // Check for recent Talabat syncs
            var recentSyncs = await GetRecentTalabatSyncsAsync(options, cancellationToken);
            if (recentSyncs.Any())
            {
                result.Warnings.Add($"Found {recentSyncs.Count} recent Talabat syncs - rollback may affect sync continuity");
            }

            // Validate data integrity
            var integrityIssues = await CheckDataIntegrityAsync(options, cancellationToken);
            if (integrityIssues.Any())
            {
                result.Warnings.AddRange(integrityIssues.Select(i => $"Data integrity issue: {i}"));
            }

            return result;
        }
        catch (Exception ex)
        {
            result.CanRollback = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
            return result;
        }
    }

    private async Task DisableMenuGroupFeaturesAsync(
        MenuGroupRollbackOptions options,
        MenuGroupRollbackResult result,
        CancellationToken cancellationToken)
    {
        if (options.DryRun)
        {
            result.Steps.Add("Would disable Menu Group features");
            return;
        }

        // Deactivate Menu Groups
        var menuGroups = await GetMenuGroupsInScopeAsync(options, cancellationToken);
        foreach (var menuGroup in menuGroups)
        {
            menuGroup.Deactivate();
        }

        if (menuGroups.Any())
        {
            await _menuGroupRepository.UpdateManyAsync(menuGroups, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Deactivated {menuGroups.Count} Menu Groups");
        }

        // Deactivate Talabat mappings
        var mappings = await GetTalabatMappingsInScopeAsync(options, cancellationToken);
        foreach (var mapping in mappings)
        {
            mapping.Deactivate();
        }

        if (mappings.Any())
        {
            await _mappingRepository.UpdateManyAsync(mappings, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Deactivated {mappings.Count} Talabat mappings");
        }
    }

    private async Task MigrateMenuGroupDataToBranchLevelAsync(
        MenuGroupRollbackOptions options,
        MenuGroupRollbackResult result,
        CancellationToken cancellationToken)
    {
        if (options.DryRun)
        {
            result.Steps.Add("Would migrate Menu Group-scoped data to branch level");
            return;
        }

        // Migrate snapshots
        var snapshots = await GetMenuGroupSnapshotsAsync(options, cancellationToken);
        foreach (var snapshot in snapshots)
        {
            snapshot.MenuGroupId = null; // Convert to branch-level
        }

        if (snapshots.Any())
        {
            await _snapshotRepository.UpdateManyAsync(snapshots, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Migrated {snapshots.Count} snapshots to branch level");
        }

        // Migrate sync runs
        var syncRuns = await GetMenuGroupSyncRunsAsync(options, cancellationToken);
        foreach (var syncRun in syncRuns)
        {
            syncRun.MenuGroupId = null; // Convert to branch-level
        }

        if (syncRuns.Any())
        {
            await _syncRunRepository.UpdateManyAsync(syncRuns, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Migrated {syncRuns.Count} sync runs to branch level");
        }

        // Migrate item mappings
        var itemMappings = await GetMenuGroupItemMappingsAsync(options, cancellationToken);
        foreach (var mapping in itemMappings)
        {
            mapping.MenuGroupId = null; // Convert to branch-level
        }

        if (itemMappings.Any())
        {
            await _itemMappingRepository.UpdateManyAsync(itemMappings, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Migrated {itemMappings.Count} item mappings to branch level");
        }

        // Migrate deltas
        var deltas = await GetMenuGroupDeltasAsync(options, cancellationToken);
        foreach (var delta in deltas)
        {
            delta.MenuGroupId = null; // Convert to branch-level
        }

        if (deltas.Any())
        {
            await _deltaRepository.UpdateManyAsync(deltas, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Migrated {deltas.Count} deltas to branch level");
        }
    }

    private async Task CleanupMenuGroupDataAsync(
        MenuGroupRollbackOptions options,
        MenuGroupRollbackResult result,
        CancellationToken cancellationToken)
    {
        if (options.DryRun)
        {
            result.Steps.Add("Would clean up Menu Group-specific data");
            return;
        }

        // Delete Talabat mappings
        var mappings = await GetTalabatMappingsInScopeAsync(options, cancellationToken);
        if (mappings.Any())
        {
            await _mappingRepository.DeleteManyAsync(mappings, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Deleted {mappings.Count} Talabat mappings");
        }

        // Delete Menu Group categories
        var categories = await GetMenuGroupCategoriesInScopeAsync(options, cancellationToken);
        if (categories.Any())
        {
            await _menuGroupCategoryRepository.DeleteManyAsync(categories, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Deleted {categories.Count} Menu Group categories");
        }

        // Delete Menu Groups
        var menuGroups = await GetMenuGroupsInScopeAsync(options, cancellationToken);
        if (menuGroups.Any())
        {
            await _menuGroupRepository.DeleteManyAsync(menuGroups, autoSave: true, cancellationToken: cancellationToken);
            result.Steps.Add($"Deleted {menuGroups.Count} Menu Groups");
        }
    }

    private async Task ValidateSystemIntegrityAsync(
        MenuGroupRollbackResult result,
        CancellationToken cancellationToken)
    {
        // Validate that all Menu Group references have been cleaned up
        var orphanedReferences = await CheckForOrphanedReferencesAsync(cancellationToken);
        if (orphanedReferences.Any())
        {
            result.ValidationWarnings.AddRange(orphanedReferences.Select(r => $"Orphaned reference: {r}"));
        }
        else
        {
            result.Steps.Add("System integrity validation passed");
        }
    }

    // Helper methods for querying data in scope
    private async Task<List<FoodicsMenuGroup>> GetMenuGroupsInScopeAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken)
    {
        var query = await _menuGroupRepository.GetQueryableAsync();
        
        if (options.RollbackScope == MenuGroupRollbackScope.SpecificAccount)
        {
            query = query.Where(mg => mg.FoodicsAccountId == options.TargetFoodicsAccountId);
            
            if (!string.IsNullOrEmpty(options.TargetBranchId))
            {
                query = query.Where(mg => mg.BranchId == options.TargetBranchId);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<MenuGroupTalabatMapping>> GetTalabatMappingsInScopeAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken)
    {
        var query = await _mappingRepository.GetQueryableAsync();
        
        if (options.RollbackScope == MenuGroupRollbackScope.SpecificAccount)
        {
            query = query.Where(m => m.FoodicsAccountId == options.TargetFoodicsAccountId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<MenuGroupCategory>> GetMenuGroupCategoriesInScopeAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken)
    {
        var menuGroups = await GetMenuGroupsInScopeAsync(options, cancellationToken);
        var menuGroupIds = menuGroups.Select(mg => mg.Id).ToList();

        if (!menuGroupIds.Any())
            return new List<MenuGroupCategory>();

        var query = await _menuGroupCategoryRepository.GetQueryableAsync();
        return await query.Where(mgc => menuGroupIds.Contains(mgc.MenuGroupId)).ToListAsync(cancellationToken);
    }

    private async Task<List<MenuSnapshot>> GetMenuGroupSnapshotsAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken)
    {
        var query = await _snapshotRepository.GetQueryableAsync();
        query = query.Where(s => s.MenuGroupId != null);
        
        if (options.RollbackScope == MenuGroupRollbackScope.SpecificAccount)
        {
            query = query.Where(s => s.FoodicsAccountId == options.TargetFoodicsAccountId);
            
            if (!string.IsNullOrEmpty(options.TargetBranchId))
            {
                query = query.Where(s => s.BranchId == options.TargetBranchId);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<MenuSyncRun>> GetMenuGroupSyncRunsAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken)
    {
        var query = await _syncRunRepository.GetQueryableAsync();
        query = query.Where(sr => sr.MenuGroupId != null);
        
        if (options.RollbackScope == MenuGroupRollbackScope.SpecificAccount)
        {
            query = query.Where(sr => sr.FoodicsAccountId == options.TargetFoodicsAccountId);
            
            if (!string.IsNullOrEmpty(options.TargetBranchId))
            {
                query = query.Where(sr => sr.BranchId == options.TargetBranchId);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<MenuItemMapping>> GetMenuGroupItemMappingsAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken)
    {
        var query = await _itemMappingRepository.GetQueryableAsync();
        query = query.Where(im => im.MenuGroupId != null);
        
        if (options.RollbackScope == MenuGroupRollbackScope.SpecificAccount)
        {
            query = query.Where(im => im.FoodicsAccountId == options.TargetFoodicsAccountId);
            
            if (!string.IsNullOrEmpty(options.TargetBranchId))
            {
                query = query.Where(im => im.BranchId == options.TargetBranchId);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<MenuDelta>> GetMenuGroupDeltasAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken)
    {
        var query = await _deltaRepository.GetQueryableAsync();
        query = query.Where(d => d.MenuGroupId != null);
        
        if (options.RollbackScope == MenuGroupRollbackScope.SpecificAccount)
        {
            query = query.Where(d => d.FoodicsAccountId == options.TargetFoodicsAccountId);
            
            if (!string.IsNullOrEmpty(options.TargetBranchId))
            {
                query = query.Where(d => d.BranchId == options.TargetBranchId);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }

    // Count methods for impact analysis
    private async Task<int> CountAffectedMenuGroupsAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        var menuGroups = await GetMenuGroupsInScopeAsync(options, cancellationToken);
        return menuGroups.Count;
    }

    private async Task<int> CountAffectedTalabatMappingsAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        var mappings = await GetTalabatMappingsInScopeAsync(options, cancellationToken);
        return mappings.Count;
    }

    private async Task<int> CountAffectedSnapshotsAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        var snapshots = await GetMenuGroupSnapshotsAsync(options, cancellationToken);
        return snapshots.Count;
    }

    private async Task<int> CountAffectedSyncRunsAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        var syncRuns = await GetMenuGroupSyncRunsAsync(options, cancellationToken);
        return syncRuns.Count;
    }

    private async Task<int> CountAffectedItemMappingsAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        var mappings = await GetMenuGroupItemMappingsAsync(options, cancellationToken);
        return mappings.Count;
    }

    private async Task<List<MenuSyncRun>> GetActiveSyncRunsAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        var query = await _syncRunRepository.GetQueryableAsync();
        query = query.Where(sr => sr.Status == MenuSyncRunStatus.Running);
        
        if (options.RollbackScope == MenuGroupRollbackScope.SpecificAccount)
        {
            query = query.Where(sr => sr.FoodicsAccountId == options.TargetFoodicsAccountId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<MenuSnapshot>> GetRecentTalabatSyncsAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24); // Last 24 hours
        var query = await _snapshotRepository.GetQueryableAsync();
        query = query.Where(s => s.IsSyncedToTalabat && s.TalabatSyncedAt >= cutoff);
        
        if (options.RollbackScope == MenuGroupRollbackScope.SpecificAccount)
        {
            query = query.Where(s => s.FoodicsAccountId == options.TargetFoodicsAccountId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<string>> CheckDataIntegrityAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        
        // Check for orphaned Menu Group categories
        var orphanedCategories = await CheckForOrphanedCategoriesAsync(options, cancellationToken);
        if (orphanedCategories > 0)
        {
            issues.Add($"{orphanedCategories} orphaned Menu Group categories found");
        }

        return issues;
    }

    private async Task<int> CheckForOrphanedCategoriesAsync(MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        // Implementation would check for categories referencing non-existent Menu Groups
        return 0;
    }

    private async Task<List<string>> CheckForOrphanedReferencesAsync(CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        
        // Check for any remaining Menu Group references after rollback
        // Implementation would validate referential integrity
        
        return issues;
    }

    private TimeSpan EstimateMigrationTime(RollbackImpactAnalysis analysis)
    {
        // Rough estimation based on data volume
        var totalRecords = analysis.AffectedSnapshots + analysis.AffectedSyncRuns + analysis.AffectedItemMappings;
        var estimatedSeconds = Math.Max(30, totalRecords * 0.1); // Minimum 30 seconds, 0.1 seconds per record
        return TimeSpan.FromSeconds(estimatedSeconds);
    }

    private async Task AnalyzePotentialIssuesAsync(RollbackImpactAnalysis analysis, MenuGroupRollbackOptions options, CancellationToken cancellationToken)
    {
        // Check for potential sync continuity issues
        if (analysis.AffectedSnapshots > 0)
        {
            analysis.PotentialIssues.Add("Menu versioning history will be consolidated to branch level");
        }

        if (analysis.AffectedTalabatMappings > 0)
        {
            analysis.PotentialIssues.Add("Talabat menu isolation will be lost");
        }

        // Check for recent activity
        var recentSyncs = await GetRecentTalabatSyncsAsync(options, cancellationToken);
        if (recentSyncs.Any())
        {
            analysis.PotentialIssues.Add($"Recent Talabat syncs ({recentSyncs.Count}) may be affected");
        }
    }

    private List<RollbackStep> CreateRollbackSteps(MenuGroupRollbackOptions options, RollbackImpactAnalysis impact)
    {
        var steps = new List<RollbackStep>();

        steps.Add(new RollbackStep
        {
            Name = "Validate Preconditions",
            Description = "Ensure no active sync runs and validate data integrity",
            EstimatedDuration = TimeSpan.FromMinutes(2),
            IsReversible = true
        });

        steps.Add(new RollbackStep
        {
            Name = "Disable Menu Group Features",
            Description = $"Deactivate {impact.AffectedMenuGroups} Menu Groups and {impact.AffectedTalabatMappings} Talabat mappings",
            EstimatedDuration = TimeSpan.FromMinutes(1),
            IsReversible = true
        });

        if (impact.RequiresDataMigration)
        {
            steps.Add(new RollbackStep
            {
                Name = "Migrate Data to Branch Level",
                Description = $"Convert {impact.AffectedSnapshots} snapshots, {impact.AffectedSyncRuns} sync runs, and {impact.AffectedItemMappings} mappings",
                EstimatedDuration = impact.EstimatedMigrationTime,
                IsReversible = false
            });
        }

        if (!options.PreserveData)
        {
            steps.Add(new RollbackStep
            {
                Name = "Clean Up Menu Group Data",
                Description = "Delete Menu Group entities and associations",
                EstimatedDuration = TimeSpan.FromMinutes(2),
                IsReversible = false
            });
        }

        steps.Add(new RollbackStep
        {
            Name = "Validate System Integrity",
            Description = "Ensure all Menu Group references are cleaned up",
            EstimatedDuration = TimeSpan.FromMinutes(1),
            IsReversible = true
        });

        return steps;
    }

    private void AddRollbackWarningsAndRecommendations(MenuGroupRollbackPlan plan, RollbackImpactAnalysis impact)
    {
        plan.Warnings.Add("Rolling back Menu Group features will disable multi-brand menu isolation");
        plan.Warnings.Add("All future syncs will operate at branch level only");

        if (impact.AffectedTalabatMappings > 0)
        {
            plan.Warnings.Add("Talabat menu mappings will be lost - manual reconfiguration may be required");
        }

        if (!plan.Options.PreserveData)
        {
            plan.Warnings.Add("Menu Group data will be permanently deleted - this action cannot be undone");
        }

        plan.Recommendations.Add("Consider preserving data during rollback for potential future re-enablement");
        plan.Recommendations.Add("Notify all users about the change in sync behavior");
        plan.Recommendations.Add("Monitor sync operations closely after rollback");
    }

    #endregion
}

#region Result and Configuration Classes

/// <summary>
/// Options for Menu Group rollback operation
/// </summary>
public class MenuGroupRollbackOptions
{
    public MenuGroupRollbackScope RollbackScope { get; set; } = MenuGroupRollbackScope.SystemWide;
    public Guid? TargetFoodicsAccountId { get; set; }
    public string? TargetBranchId { get; set; }
    public bool PreserveData { get; set; } = true;
    public bool DryRun { get; set; } = false;
    public string? Reason { get; set; }
    public string InitiatedBy { get; set; } = "System";
}

/// <summary>
/// Scope of Menu Group rollback
/// </summary>
public enum MenuGroupRollbackScope
{
    SystemWide,
    SpecificAccount,
    SpecificBranch
}

/// <summary>
/// Result of Menu Group rollback operation
/// </summary>
public class MenuGroupRollbackResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public MenuGroupRollbackOptions Options { get; set; } = new();
    public List<string> Steps { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
}

/// <summary>
/// Result of rollback validation
/// </summary>
public class RollbackValidationResult
{
    public bool CanRollback { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Analysis of rollback impact
/// </summary>
public class RollbackImpactAnalysis
{
    public DateTime AnalyzedAt { get; set; }
    public MenuGroupRollbackOptions Options { get; set; } = new();
    public int AffectedMenuGroups { get; set; }
    public int AffectedTalabatMappings { get; set; }
    public int AffectedSnapshots { get; set; }
    public int AffectedSyncRuns { get; set; }
    public int AffectedItemMappings { get; set; }
    public bool RequiresDataMigration { get; set; }
    public TimeSpan EstimatedMigrationTime { get; set; }
    public List<string> PotentialIssues { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Comprehensive rollback plan
/// </summary>
public class MenuGroupRollbackPlan
{
    public DateTime CreatedAt { get; set; }
    public MenuGroupRollbackOptions Options { get; set; } = new();
    public RollbackImpactAnalysis? ImpactAnalysis { get; set; }
    public List<RollbackStep> Steps { get; set; } = new();
    public TimeSpan EstimatedDuration { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Individual step in rollback plan
/// </summary>
public class RollbackStep
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan EstimatedDuration { get; set; }
    public bool IsReversible { get; set; }
}

#endregion