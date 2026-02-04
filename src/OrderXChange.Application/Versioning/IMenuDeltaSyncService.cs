using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Versioning;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service interface for menu delta synchronization
/// Handles efficient incremental sync between Foodics and Talabat
/// </summary>
public interface IMenuDeltaSyncService
{
    /// <summary>
    /// Generates a delta between current menu and latest snapshot
    /// Returns null if no changes detected
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="currentProducts">Current menu products from Foodics</param>
    /// <param name="menuGroupId">Menu Group ID for scoped sync (null for branch-level sync)</param>
    /// <param name="forceFullSync">Force full sync even if incremental is possible</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delta generation result</returns>
    Task<DeltaGenerationResult> GenerateDeltaAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        Guid? menuGroupId = null,
        bool forceFullSync = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a delta to Talabat using partial payload
    /// Handles dependency resolution and partial failure recovery
    /// </summary>
    /// <param name="deltaId">Delta ID to sync</param>
    /// <param name="talabatVendorCode">Target Talabat vendor code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync result</returns>
    Task<DeltaSyncResult> SyncDeltaToTalabatAsync(
        Guid deltaId,
        string talabatVendorCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs complete delta sync workflow: generate + sync
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="currentProducts">Current menu products from Foodics</param>
    /// <param name="talabatVendorCode">Target Talabat vendor code</param>
    /// <param name="menuGroupId">Menu Group ID for scoped sync (null for branch-level sync)</param>
    /// <param name="forceFullSync">Force full sync even if incremental is possible</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Combined generation and sync result</returns>
    Task<DeltaSyncResult> GenerateAndSyncDeltaAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        string talabatVendorCode,
        Guid? menuGroupId = null,
        bool forceFullSync = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a delta payload by ID
    /// Decompresses and deserializes the stored payload
    /// </summary>
    /// <param name="deltaId">Delta ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delta payload</returns>
    Task<MenuDeltaPayload?> GetDeltaPayloadAsync(
        Guid deltaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending deltas for a specific account/branch/menu group
    /// Used for retry mechanisms and monitoring
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="menuGroupId">Menu Group ID (null for branch-level deltas)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending deltas</returns>
    Task<List<MenuDelta>> GetPendingDeltasAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        Guid? menuGroupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a failed delta sync
    /// Implements exponential backoff and partial failure handling
    /// </summary>
    /// <param name="deltaId">Delta ID to retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Retry result</returns>
    Task<DeltaSyncResult> RetryDeltaSyncAsync(
        Guid deltaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates delta payload integrity
    /// Checks for missing dependencies and data consistency
    /// </summary>
    /// <param name="payload">Delta payload to validate</param>
    /// <returns>Validation result with warnings/errors</returns>
    Task<DeltaValidationResult> ValidateDeltaPayloadAsync(MenuDeltaPayload payload);

    /// <summary>
    /// Gets delta statistics for monitoring and analytics
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="fromDate">Start date for statistics</param>
    /// <param name="toDate">End date for statistics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delta statistics</returns>
    Task<DeltaStatisticsReport> GetDeltaStatisticsAsync(
        Guid foodicsAccountId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs delta cleanup - removes old deltas and compressed payloads
    /// Keeps only recent deltas based on retention policy
    /// </summary>
    /// <param name="retentionDays">Number of days to retain deltas</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cleanup statistics</returns>
    Task<DeltaCleanupResult> CleanupOldDeltasAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes high-performance batch sync with optimization
    /// Uses parallel processing, payload compression, and intelligent batching
    /// </summary>
    /// <param name="deltaIds">List of delta IDs to sync</param>
    /// <param name="talabatVendorCode">Target Talabat vendor code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch sync result with performance metrics</returns>
    Task<ParallelSyncResult> ExecuteHighPerformanceBatchSyncAsync(
        List<Guid> deltaIds,
        string talabatVendorCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes and compresses delta payload for minimal size
    /// Reduces network overhead and improves sync performance
    /// </summary>
    /// <param name="deltaId">Delta ID to optimize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimized payload with compression statistics</returns>
    Task<OptimizedMenuDeltaPayload> OptimizeDeltaPayloadAsync(
        Guid deltaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets performance recommendations for menu sync optimization
    /// Analyzes historical data to suggest optimal settings
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Performance recommendations</returns>
    Task<MenuSyncPerformanceRecommendations> GetPerformanceRecommendationsAsync(
        Guid foodicsAccountId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Delta validation result
/// </summary>
public class DeltaValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> MissingDependencies { get; set; } = new();
}

/// <summary>
/// Delta statistics report
/// </summary>
public class DeltaStatisticsReport
{
    public Guid FoodicsAccountId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalDeltas { get; set; }
    public int SuccessfulSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public int PendingSyncs { get; set; }
    public double AverageSyncTime { get; set; }
    public long TotalDataSynced { get; set; }
    public double CompressionRatio { get; set; }
    public Dictionary<string, int> ChangeTypeBreakdown { get; set; } = new();
}

/// <summary>
/// Delta cleanup result
/// </summary>
public class DeltaCleanupResult
{
    public int DeletedDeltas { get; set; }
    public long FreedStorageBytes { get; set; }
    public TimeSpan CleanupTime { get; set; }
    public List<string> Errors { get; set; } = new();
}