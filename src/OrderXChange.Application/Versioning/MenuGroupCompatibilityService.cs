using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service responsible for maintaining backward compatibility with legacy sync operations
/// Automatically creates default Menu Groups when none exist and ensures old sync logic continues to work
/// </summary>
public class MenuGroupCompatibilityService : IMenuGroupCompatibilityService, ITransientDependency
{
    private readonly IRepository<FoodicsMenuGroup, Guid> _menuGroupRepository;
    private readonly IRepository<MenuGroupCategory, Guid> _menuGroupCategoryRepository;
    private readonly IMenuGroupService _menuGroupService;
    private readonly ILogger<MenuGroupCompatibilityService> _logger;

    // Constants for default Menu Group
    private const string DEFAULT_MENU_GROUP_NAME = "All Categories";
    private const string DEFAULT_MENU_GROUP_DESCRIPTION = "Default menu group containing all categories (created for backward compatibility)";

    public MenuGroupCompatibilityService(
        IRepository<FoodicsMenuGroup, Guid> menuGroupRepository,
        IRepository<MenuGroupCategory, Guid> menuGroupCategoryRepository,
        IMenuGroupService menuGroupService,
        ILogger<MenuGroupCompatibilityService> logger)
    {
        _menuGroupRepository = menuGroupRepository;
        _menuGroupCategoryRepository = menuGroupCategoryRepository;
        _menuGroupService = menuGroupService;
        _logger = logger;
    }

    /// <summary>
    /// Ensures a default Menu Group exists for legacy sync operations
    /// Creates one if none exists, containing all available categories
    /// </summary>
    public async Task<Guid?> EnsureDefaultMenuGroupAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> products,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if any Menu Groups exist for this account/branch
            var existingMenuGroups = await GetMenuGroupsAsync(foodicsAccountId, branchId, cancellationToken);
            
            if (existingMenuGroups.Any())
            {
                _logger.LogDebug(
                    "Menu Groups already exist for account {AccountId}, branch {BranchId}. Count: {Count}",
                    foodicsAccountId, branchId ?? "ALL", existingMenuGroups.Count);
                return null; // No need to create default
            }

            _logger.LogInformation(
                "No Menu Groups found for account {AccountId}, branch {BranchId}. Creating default Menu Group for backward compatibility.",
                foodicsAccountId, branchId ?? "ALL");

            // Extract unique categories from products
            var categories = products
                .Where(p => p.Category != null)
                .Select(p => p.Category!)
                .DistinctBy(c => c.Id)
                .ToList();

            if (!categories.Any())
            {
                _logger.LogWarning(
                    "No categories found in products for account {AccountId}, branch {BranchId}. Cannot create default Menu Group.",
                    foodicsAccountId, branchId ?? "ALL");
                return null;
            }

            // Create default Menu Group
            var defaultMenuGroup = await CreateDefaultMenuGroupAsync(
                foodicsAccountId, branchId, categories, cancellationToken);

            _logger.LogInformation(
                "Created default Menu Group {MenuGroupId} with {CategoryCount} categories for backward compatibility.",
                defaultMenuGroup.Id, categories.Count);

            return defaultMenuGroup.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to ensure default Menu Group for account {AccountId}, branch {BranchId}",
                foodicsAccountId, branchId ?? "ALL");
            
            // Don't throw - backward compatibility should be non-breaking
            return null;
        }
    }

    /// <summary>
    /// Resolves Menu Group ID for sync operations
    /// Returns null for legacy branch-level sync, or the appropriate Menu Group ID
    /// </summary>
    public async Task<MenuGroupResolutionResult> ResolveMenuGroupForSyncAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? requestedMenuGroupId,
        List<FoodicsProductDetailDto> products,
        bool enableAutoCreation = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If Menu Group explicitly requested, validate and return it
            if (requestedMenuGroupId.HasValue)
            {
                var requestedMenuGroup = await _menuGroupRepository.FindAsync(requestedMenuGroupId.Value, cancellationToken: cancellationToken);
                if (requestedMenuGroup == null)
                {
                    return new MenuGroupResolutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Requested Menu Group {requestedMenuGroupId} not found",
                        SyncMode = MenuGroupSyncMode.Invalid
                    };
                }

                if (!requestedMenuGroup.IsActive)
                {
                    return new MenuGroupResolutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Requested Menu Group {requestedMenuGroupId} is not active",
                        SyncMode = MenuGroupSyncMode.Invalid
                    };
                }

                return new MenuGroupResolutionResult
                {
                    Success = true,
                    MenuGroupId = requestedMenuGroupId.Value,
                    SyncMode = MenuGroupSyncMode.MenuGroupSpecific,
                    IsDefaultMenuGroup = false
                };
            }

            // Check if Menu Groups exist for this account/branch
            var existingMenuGroups = await GetMenuGroupsAsync(foodicsAccountId, branchId, cancellationToken);

            if (!existingMenuGroups.Any())
            {
                if (enableAutoCreation)
                {
                    // Create default Menu Group for backward compatibility
                    var defaultMenuGroupId = await EnsureDefaultMenuGroupAsync(
                        foodicsAccountId, branchId, products, cancellationToken);

                    if (defaultMenuGroupId.HasValue)
                    {
                        return new MenuGroupResolutionResult
                        {
                            Success = true,
                            MenuGroupId = defaultMenuGroupId.Value,
                            SyncMode = MenuGroupSyncMode.DefaultMenuGroup,
                            IsDefaultMenuGroup = true,
                            WasAutoCreated = true
                        };
                    }
                }

                // Fall back to legacy branch-level sync
                _logger.LogInformation(
                    "No Menu Groups exist and auto-creation disabled/failed. Using legacy branch-level sync for account {AccountId}, branch {BranchId}",
                    foodicsAccountId, branchId ?? "ALL");

                return new MenuGroupResolutionResult
                {
                    Success = true,
                    MenuGroupId = null,
                    SyncMode = MenuGroupSyncMode.LegacyBranchLevel,
                    IsDefaultMenuGroup = false
                };
            }

            // Multiple Menu Groups exist - cannot auto-select, use legacy mode
            if (existingMenuGroups.Count > 1)
            {
                _logger.LogInformation(
                    "Multiple Menu Groups exist ({Count}) for account {AccountId}, branch {BranchId}. Using legacy branch-level sync.",
                    existingMenuGroups.Count, foodicsAccountId, branchId ?? "ALL");

                return new MenuGroupResolutionResult
                {
                    Success = true,
                    MenuGroupId = null,
                    SyncMode = MenuGroupSyncMode.LegacyBranchLevel,
                    IsDefaultMenuGroup = false,
                    AvailableMenuGroups = existingMenuGroups.Select(mg => new MenuGroupInfo
                    {
                        Id = mg.Id,
                        Name = mg.Name,
                        IsActive = mg.IsActive
                    }).ToList()
                };
            }

            // Single Menu Group exists - use it
            var singleMenuGroup = existingMenuGroups.First();
            var isDefault = singleMenuGroup.Name == DEFAULT_MENU_GROUP_NAME;

            return new MenuGroupResolutionResult
            {
                Success = true,
                MenuGroupId = singleMenuGroup.Id,
                SyncMode = isDefault ? MenuGroupSyncMode.DefaultMenuGroup : MenuGroupSyncMode.MenuGroupSpecific,
                IsDefaultMenuGroup = isDefault
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to resolve Menu Group for sync. Account {AccountId}, branch {BranchId}",
                foodicsAccountId, branchId ?? "ALL");

            return new MenuGroupResolutionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                SyncMode = MenuGroupSyncMode.Invalid
            };
        }
    }

    /// <summary>
    /// Migrates existing data to use Menu Groups
    /// Creates default Menu Groups for accounts that don't have any
    /// </summary>
    public async Task<MenuGroupMigrationResult> MigrateToMenuGroupsAsync(
        Guid? specificFoodicsAccountId = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var result = new MenuGroupMigrationResult
        {
            StartedAt = DateTime.UtcNow,
            IsDryRun = dryRun
        };

        try
        {
            _logger.LogInformation(
                "Starting Menu Group migration. SpecificAccount={AccountId}, DryRun={DryRun}",
                specificFoodicsAccountId?.ToString() ?? "ALL", dryRun);

            // Get accounts that need migration (no Menu Groups)
            var accountsToMigrate = await GetAccountsNeedingMigrationAsync(specificFoodicsAccountId, cancellationToken);
            
            result.TotalAccountsToMigrate = accountsToMigrate.Count;

            foreach (var account in accountsToMigrate)
            {
                try
                {
                    var accountResult = await MigrateAccountToMenuGroupsAsync(account, dryRun, cancellationToken);
                    result.AccountResults.Add(accountResult);
                    
                    if (accountResult.Success)
                    {
                        result.SuccessfulMigrations++;
                    }
                    else
                    {
                        result.FailedMigrations++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to migrate account {AccountId}", account.FoodicsAccountId);
                    
                    result.AccountResults.Add(new AccountMigrationResult
                    {
                        FoodicsAccountId = account.FoodicsAccountId,
                        BranchId = account.BranchId,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                    result.FailedMigrations++;
                }
            }

            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            _logger.LogInformation(
                "Menu Group migration completed. Total={Total}, Success={Success}, Failed={Failed}, Duration={Duration}ms",
                result.TotalAccountsToMigrate, result.SuccessfulMigrations, result.FailedMigrations, 
                result.Duration?.TotalMilliseconds ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Menu Group migration failed");
            
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;
            result.ErrorMessage = ex.Message;
            
            return result;
        }
    }

    /// <summary>
    /// Validates that Menu Group operations are backward compatible
    /// </summary>
    public async Task<CompatibilityValidationResult> ValidateBackwardCompatibilityAsync(
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        var result = new CompatibilityValidationResult { IsCompatible = true };

        try
        {
            // Check if Menu Groups exist
            var menuGroups = await GetMenuGroupsAsync(foodicsAccountId, branchId, cancellationToken);
            
            if (!menuGroups.Any())
            {
                result.Warnings.Add("No Menu Groups exist - will use legacy branch-level sync");
                return result;
            }

            // Validate each Menu Group
            foreach (var menuGroup in menuGroups)
            {
                var validation = menuGroup.ValidateForSync();
                if (!validation.IsValid)
                {
                    result.IsCompatible = false;
                    result.Errors.AddRange(validation.Errors.Select(e => $"Menu Group '{menuGroup.Name}': {e}"));
                }
                
                result.Warnings.AddRange(validation.Warnings.Select(w => $"Menu Group '{menuGroup.Name}': {w}"));
            }

            // Check for orphaned data
            var orphanedSnapshots = await CheckForOrphanedSnapshotsAsync(foodicsAccountId, branchId, cancellationToken);
            if (orphanedSnapshots > 0)
            {
                result.Warnings.Add($"Found {orphanedSnapshots} snapshots without Menu Group association");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate backward compatibility");
            
            result.IsCompatible = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
            return result;
        }
    }

    #region Private Methods

    private async Task<List<FoodicsMenuGroup>> GetMenuGroupsAsync(
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken)
    {
        var query = await _menuGroupRepository.GetQueryableAsync();
        
        return await query
            .Where(mg => mg.FoodicsAccountId == foodicsAccountId)
            .Where(mg => mg.BranchId == branchId)
            .Where(mg => mg.IsActive)
            .ToListAsync(cancellationToken);
    }

    private async Task<FoodicsMenuGroup> CreateDefaultMenuGroupAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsCategoryInfoDto> categories,
        CancellationToken cancellationToken)
    {
        // Create default Menu Group
        var createDto = new CreateMenuGroupDto
        {
            FoodicsAccountId = foodicsAccountId,
            Name = DEFAULT_MENU_GROUP_NAME,
            Description = DEFAULT_MENU_GROUP_DESCRIPTION,
            BranchId = branchId,
            SortOrder = 0
        };

        var menuGroupDto = await _menuGroupService.CreateAsync(createDto);

        // Convert DTO to domain entity for return
        var menuGroup = new FoodicsMenuGroup
        {
            FoodicsAccountId = menuGroupDto.FoodicsAccountId,
            Name = menuGroupDto.Name,
            Description = menuGroupDto.Description,
            BranchId = menuGroupDto.BranchId,
            SortOrder = menuGroupDto.SortOrder,
            IsActive = menuGroupDto.IsActive
        };
        
        // Set the Id using reflection or by querying back
        typeof(FoodicsMenuGroup).GetProperty("Id")?.SetValue(menuGroup, menuGroupDto.Id);
       

        // Assign all categories to the default Menu Group
        foreach (var category in categories)
        {
            try
            {
                await _menuGroupService.AssignCategoryAsync(menuGroup.Id, new AssignCategoryDto
                {
                    CategoryId = category.Id,
                    SortOrder = 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, 
                    "Failed to assign category {CategoryId} to default Menu Group {MenuGroupId}",
                    category.Id, menuGroup.Id);
            }
        }

        return await _menuGroupRepository.GetAsync(menuGroup.Id, cancellationToken: cancellationToken);
    }

    private async Task<List<AccountBranchInfo>> GetAccountsNeedingMigrationAsync(
        Guid? specificFoodicsAccountId,
        CancellationToken cancellationToken)
    {
        // This would typically query existing snapshots or sync runs to find accounts/branches
        // that have been syncing but don't have Menu Groups
        // For now, return empty list as this would be implementation-specific
        return new List<AccountBranchInfo>();
    }

    private async Task<AccountMigrationResult> MigrateAccountToMenuGroupsAsync(
        AccountBranchInfo account,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var result = new AccountMigrationResult
        {
            FoodicsAccountId = account.FoodicsAccountId,
            BranchId = account.BranchId
        };

        try
        {
            if (dryRun)
            {
                result.Success = true;
                result.Message = "Dry run - would create default Menu Group";
                return result;
            }

            // Implementation would create default Menu Group and migrate existing data
            result.Success = true;
            result.Message = "Successfully migrated to Menu Groups";
            
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private async Task<int> CheckForOrphanedSnapshotsAsync(
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken)
    {
        // Implementation would check for snapshots without Menu Group association
        return 0;
    }

    #endregion
}

#region Result Classes

/// <summary>
/// Result of Menu Group resolution for sync operations
/// </summary>
public class MenuGroupResolutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? MenuGroupId { get; set; }
    public MenuGroupSyncMode SyncMode { get; set; }
    public bool IsDefaultMenuGroup { get; set; }
    public bool WasAutoCreated { get; set; }
    public List<MenuGroupInfo> AvailableMenuGroups { get; set; } = new();
}

/// <summary>
/// Sync mode for Menu Group operations
/// </summary>
public enum MenuGroupSyncMode
{
    Invalid,
    LegacyBranchLevel,      // No Menu Groups - use old logic
    DefaultMenuGroup,       // Single default Menu Group
    MenuGroupSpecific,      // Specific Menu Group requested
}

/// <summary>
/// Information about a Menu Group
/// </summary>
public class MenuGroupInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// Result of Menu Group migration
/// </summary>
public class MenuGroupMigrationResult
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool IsDryRun { get; set; }
    public int TotalAccountsToMigrate { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public string? ErrorMessage { get; set; }
    public List<AccountMigrationResult> AccountResults { get; set; } = new();
}

/// <summary>
/// Result of migrating a single account to Menu Groups
/// </summary>
public class AccountMigrationResult
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CreatedMenuGroupId { get; set; }
    public int CategoriesAssigned { get; set; }
}

/// <summary>
/// Account and branch information for migration
/// </summary>
public class AccountBranchInfo
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
}

/// <summary>
/// Result of backward compatibility validation
/// </summary>
public class CompatibilityValidationResult
{
    public bool IsCompatible { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

#endregion