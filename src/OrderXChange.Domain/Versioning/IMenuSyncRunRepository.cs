using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Repository interface for MenuSyncRun aggregate root
/// Provides specialized queries for sync run management and monitoring
/// </summary>
public interface IMenuSyncRunRepository : IRepository<MenuSyncRun, Guid>
{
    /// <summary>
    /// Gets active sync runs for a specific Foodics account
    /// </summary>
    Task<List<MenuSyncRun>> GetActiveSyncRunsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest sync run for a specific account/branch
    /// </summary>
    Task<MenuSyncRun?> GetLatestSyncRunAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sync runs by correlation ID (for distributed tracing)
    /// </summary>
    Task<List<MenuSyncRun>> GetSyncRunsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed sync runs that can be retried
    /// </summary>
    Task<List<MenuSyncRun>> GetRetryableSyncRunsAsync(
        int maxRetryCount = 3,
        TimeSpan? minTimeSinceLastAttempt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sync runs within a date range with optional filtering
    /// </summary>
    Task<List<MenuSyncRun>> GetSyncRunsInRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        Guid? foodicsAccountId = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sync run statistics for monitoring and reporting
    /// </summary>
    Task<MenuSyncRunStatistics> GetSyncRunStatisticsAsync(
        Guid foodicsAccountId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets long-running sync runs that may need attention
    /// </summary>
    Task<List<MenuSyncRun>> GetLongRunningSyncRunsAsync(
        TimeSpan threshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sync runs with high error rates
    /// </summary>
    Task<List<MenuSyncRun>> GetHighErrorRateSyncRunsAsync(
        double errorRateThreshold = 0.1,
        DateTime? fromDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old completed sync runs
    /// </summary>
    Task<int> CleanupOldSyncRunsAsync(
        int retentionDays = 30,
        bool keepFailedRuns = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for sync runs
/// </summary>
public class MenuSyncRunStatistics
{
    public Guid FoodicsAccountId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalRuns { get; set; }
    public int SuccessfulRuns { get; set; }
    public int FailedRuns { get; set; }
    public int CancelledRuns { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public int TotalProductsProcessed { get; set; }
    public int TotalProductsSucceeded { get; set; }
    public int TotalProductsFailed { get; set; }
    public double AverageProductSuccessRate { get; set; }
    public Dictionary<string, int> RunsByType { get; set; } = new();
    public Dictionary<string, int> RunsByTriggerSource { get; set; } = new();
    public List<string> CommonErrors { get; set; } = new();
}