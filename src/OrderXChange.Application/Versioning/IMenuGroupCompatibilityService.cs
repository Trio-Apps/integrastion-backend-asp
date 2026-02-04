using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Interface for Menu Group backward compatibility service
/// </summary>
public interface IMenuGroupCompatibilityService
{
    /// <summary>
    /// Ensures a default Menu Group exists for legacy sync operations
    /// </summary>
    Task<Guid?> EnsureDefaultMenuGroupAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> products,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves Menu Group ID for sync operations
    /// </summary>
    Task<MenuGroupResolutionResult> ResolveMenuGroupForSyncAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? requestedMenuGroupId,
        List<FoodicsProductDetailDto> products,
        bool enableAutoCreation = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrates existing data to use Menu Groups
    /// </summary>
    Task<MenuGroupMigrationResult> MigrateToMenuGroupsAsync(
        Guid? specificFoodicsAccountId = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that Menu Group operations are backward compatible
    /// </summary>
    Task<CompatibilityValidationResult> ValidateBackwardCompatibilityAsync(
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken = default);
}