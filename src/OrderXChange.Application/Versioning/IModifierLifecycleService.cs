using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Interface for managing modifier lifecycle including tracking, versioning, and price changes
/// </summary>
public interface IModifierLifecycleService
{
    /// <summary>
    /// Synchronizes modifier groups and options from Foodics data
    /// </summary>
    Task<ModifierSyncResult> SyncModifiersAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId,
        List<FoodicsProductDetailDto> products,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates modifier group structure and handles versioning
    /// </summary>
    Task<ModifierGroup> UpdateModifierGroupAsync(
        Guid modifierGroupId,
        string name,
        string? nameLocalized,
        int? minSelection,
        int? maxSelection,
        bool isRequired,
        string? changeReason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates modifier option price with history tracking
    /// </summary>
    Task<ModifierOption> UpdateModifierOptionPriceAsync(
        Guid modifierOptionId,
        decimal newPrice,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets modifier groups for a specific context
    /// </summary>
    Task<List<ModifierGroup>> GetModifierGroupsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        Guid? menuGroupId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets modifier options for a specific modifier group
    /// </summary>
    Task<List<ModifierOption>> GetModifierOptionsAsync(
        Guid modifierGroupId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets price history for a modifier option
    /// </summary>
    Task<List<ModifierOptionPriceHistory>> GetPriceHistoryAsync(
        Guid modifierOptionId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects modifier changes between current and previous versions
    /// </summary>
    Task<ModifierChangeDetectionResult> DetectModifierChangesAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId,
        List<FoodicsProductDetailDto> currentProducts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets modifier groups that need to be synced to Talabat
    /// </summary>
    Task<List<ModifierGroup>> GetModifierGroupsNeedingSyncAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        Guid? menuGroupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks modifier groups as synced to Talabat
    /// </summary>
    Task MarkModifierGroupsAsSyncedAsync(
        List<Guid> modifierGroupIds,
        string talabatVendorCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates modifier configuration for Talabat compliance
    /// </summary>
    Task<ModifierValidationResult> ValidateModifiersForTalabatAsync(
        List<ModifierGroup> modifierGroups,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets modifier analytics and insights
    /// </summary>
    Task<ModifierAnalyticsResult> GetModifierAnalyticsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        Guid? menuGroupId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback modifier group to a previous version
    /// </summary>
    Task<ModifierGroup> RollbackModifierGroupAsync(
        Guid modifierGroupId,
        int targetVersion,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback modifier option to a previous version
    /// </summary>
    Task<ModifierOption> RollbackModifierOptionAsync(
        Guid modifierOptionId,
        int targetVersion,
        string? reason = null,
        CancellationToken cancellationToken = default);
}