using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Interface for Menu Group sync adapter that provides backward compatibility
/// </summary>
public interface IMenuGroupSyncAdapter
{
    /// <summary>
    /// Legacy sync method that automatically handles Menu Group resolution
    /// </summary>
    Task<MenuSyncRun> ExecuteLegacySyncAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        string talabatVendorCode,
        string syncType = MenuSyncRunType.Manual,
        string triggerSource = MenuSyncTriggerSource.User,
        string initiatedBy = "System",
        bool forceFullSync = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enhanced sync method that accepts optional Menu Group ID
    /// </summary>
    Task<MenuSyncRun> ExecuteEnhancedSyncAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a sync operation will be backward compatible
    /// </summary>
    Task<SyncCompatibilityCheck> CheckSyncCompatibilityAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sync recommendations for transitioning to Menu Groups
    /// </summary>
    Task<MenuGroupTransitionRecommendation> GetTransitionRecommendationAsync(
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken = default);
}