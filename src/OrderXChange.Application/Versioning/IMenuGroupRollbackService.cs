using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Interface for Menu Group rollback service
/// </summary>
public interface IMenuGroupRollbackService
{
    /// <summary>
    /// Performs a complete rollback of Menu Group features
    /// </summary>
    Task<MenuGroupRollbackResult> ExecuteFullRollbackAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a partial rollback for specific accounts or branches
    /// </summary>
    Task<MenuGroupRollbackResult> ExecutePartialRollbackAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        bool preserveData = true,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a rollback can be performed safely
    /// </summary>
    Task<RollbackValidationResult> ValidateRollbackAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets rollback impact analysis
    /// </summary>
    Task<RollbackImpactAnalysis> AnalyzeRollbackImpactAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a rollback plan with detailed steps
    /// </summary>
    Task<MenuGroupRollbackPlan> CreateRollbackPlanAsync(
        MenuGroupRollbackOptions options,
        CancellationToken cancellationToken = default);
}