using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.Application.Services;

namespace OrderXChange.BackgroundJobs;

/// <summary>
/// Application service interface for Menu Sync operations
/// </summary>
public interface IMenuSyncAppService : IApplicationService
{
    /// <summary>
    /// Manually trigger a menu sync for a specific account
    /// </summary>
    Task TriggerMenuSyncAsync(
        Guid? foodicsAccountId = null,
        string? branchId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated menu by fetching all products with full includes and building menu structure from products.
    /// Groups products by category and by custom groups.
    /// </summary>
    /// <param name="branchId">Optional branch ID to filter products</param>
    /// <param name="foodicsAccountId">Optional FoodicsAccount ID. If not provided, uses current tenant's account or configuration token</param>
    Task<FoodicsAggregatedMenuDto> GetAggregatedAsync(string? branchId = null, Guid? foodicsAccountId = null);

    /// <summary>
    /// Gets enhanced aggregated menu with detailed branch-level analysis.
    /// Shows categories, menu groups, and products breakdown for each branch.
    /// </summary>
    /// <param name="request">Request parameters for enhanced analysis</param>
    Task<FoodicsEnhancedAggregatedMenuDto> GetEnhancedAggregatedAsync(GetEnhancedAggregatedMenuRequest request);

    /// <summary>
    /// Legacy method - kept for backward compatibility
    /// </summary>
    Task<FoodicsMenuDisplayResponseDto> GetAsync(string? branchId = null);

    /// <summary>
    /// Gets available active branches for a specific FoodicsAccount.
    /// Only returns branches where is_active is true.
    /// Used for dropdown selection when configuring TalabatAccount.
    /// </summary>
    /// <param name="foodicsAccountId">FoodicsAccount ID to get branches for</param>
    Task<List<FoodicsBranchDto>> GetBranchesForAccountAsync(Guid foodicsAccountId);

    /// <summary>
    /// Gets available groups for a specific FoodicsAccount.
    /// Returns all unique groups that have products assigned to them.
    /// Used for dropdown selection when configuring TalabatAccount group filtering.
    /// </summary>
    /// <param name="foodicsAccountId">FoodicsAccount ID to get groups for</param>
    Task<List<FoodicsGroupWithProductCountDto>> GetGroupsForAccountAsync(Guid foodicsAccountId);

    /// <summary>
    /// Reads menu group summary from AppFoodicsProductStaging (database) for active products.
    /// Returns totals per menu group: total products, total categories, and product names list.
    /// </summary>
    Task<List<StagingMenuGroupSummaryDto>> GetStagingMenuGroupSummaryAsync(GetStagingMenuGroupSummaryRequest request);
}
