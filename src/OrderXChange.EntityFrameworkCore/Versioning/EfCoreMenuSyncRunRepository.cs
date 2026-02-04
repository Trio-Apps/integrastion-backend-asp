using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Domain.Versioning;
using OrderXChange.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace OrderXChange.EntityFrameworkCore.Versioning;

/// <summary>
/// Entity Framework Core implementation of MenuSyncRun repository
/// Provides optimized queries for sync run management and monitoring
/// </summary>
public class EfCoreMenuSyncRunRepository : EfCoreRepository<OrderXChangeDbContext, MenuSyncRun, Guid>, IMenuSyncRunRepository
{
    public EfCoreMenuSyncRunRepository(IDbContextProvider<OrderXChangeDbContext> dbContextProvider) 
        : base(dbContextProvider)
    {
    }

    public async Task<List<MenuSyncRun>> GetActiveSyncRunsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        
        return await dbSet
            .Where(sr => sr.FoodicsAccountId == foodicsAccountId)
            .Where(sr => branchId == null || sr.BranchId == branchId)
            .Where(sr => sr.Status == MenuSyncRunStatus.Running || sr.Status == MenuSyncRunStatus.Pending)
            .OrderByDescending(sr => sr.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<MenuSyncRun?> GetLatestSyncRunAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        
        return await dbSet
            .Where(sr => sr.FoodicsAccountId == foodicsAccountId)
            .Where(sr => branchId == null || sr.BranchId == branchId)
            .OrderByDescending(sr => sr.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<MenuSyncRun>> GetSyncRunsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        
        return await dbSet
            .Where(sr => sr.CorrelationId == correlationId)
            .OrderBy(sr => sr.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MenuSyncRun>> GetRetryableSyncRunsAsync(
        int maxRetryCount = 3,
        TimeSpan? minTimeSinceLastAttempt = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var cutoffTime = minTimeSinceLastAttempt.HasValue 
            ? DateTime.UtcNow - minTimeSinceLastAttempt.Value 
            : DateTime.UtcNow.AddMinutes(-30); // Default 30 minutes

        return await dbSet
            .Where(sr => sr.Status == MenuSyncRunStatus.Failed)
            .Where(sr => sr.CanRetry)
            .Where(sr => sr.RetryCount < maxRetryCount)
            .Where(sr => sr.CompletedAt.HasValue && sr.CompletedAt.Value < cutoffTime)
            .OrderBy(sr => sr.RetryCount)
            .ThenBy(sr => sr.CompletedAt)
            .Take(50) // Limit to prevent overwhelming the system
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MenuSyncRun>> GetSyncRunsInRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        Guid? foodicsAccountId = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        
        var query = dbSet
            .Where(sr => sr.StartedAt >= fromDate && sr.StartedAt <= toDate);

        if (foodicsAccountId.HasValue)
        {
            query = query.Where(sr => sr.FoodicsAccountId == foodicsAccountId.Value);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(sr => sr.Status == status);
        }

        return await query
            .OrderByDescending(sr => sr.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<MenuSyncRunStatistics> GetSyncRunStatisticsAsync(
        Guid foodicsAccountId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        
        var syncRuns = await dbSet
            .Where(sr => sr.FoodicsAccountId == foodicsAccountId)
            .Where(sr => sr.StartedAt >= fromDate && sr.StartedAt <= toDate)
            .ToListAsync(cancellationToken);

        var completedRuns = syncRuns.Where(sr => sr.Duration.HasValue).ToList();

        var statistics = new MenuSyncRunStatistics
        {
            FoodicsAccountId = foodicsAccountId,
            FromDate = fromDate,
            ToDate = toDate,
            TotalRuns = syncRuns.Count,
            SuccessfulRuns = syncRuns.Count(sr => sr.Status == MenuSyncRunStatus.Completed),
            FailedRuns = syncRuns.Count(sr => sr.Status == MenuSyncRunStatus.Failed),
            CancelledRuns = syncRuns.Count(sr => sr.Status == MenuSyncRunStatus.Cancelled),
            TotalProductsProcessed = syncRuns.Sum(sr => sr.TotalProductsProcessed),
            TotalProductsSucceeded = syncRuns.Sum(sr => sr.ProductsSucceeded),
            TotalProductsFailed = syncRuns.Sum(sr => sr.ProductsFailed)
        };

        // Calculate rates
        statistics.SuccessRate = statistics.TotalRuns > 0 
            ? (double)statistics.SuccessfulRuns / statistics.TotalRuns * 100 
            : 0;

        statistics.AverageProductSuccessRate = statistics.TotalProductsProcessed > 0 
            ? (double)statistics.TotalProductsSucceeded / statistics.TotalProductsProcessed * 100 
            : 0;

        // Calculate durations
        if (completedRuns.Any())
        {
            statistics.AverageDuration = TimeSpan.FromTicks((long)completedRuns.Average(sr => sr.Duration!.Value.Ticks));
            statistics.MaxDuration = completedRuns.Max(sr => sr.Duration!.Value);
            statistics.MinDuration = completedRuns.Min(sr => sr.Duration!.Value);
        }

        // Group by type and trigger source
        statistics.RunsByType = syncRuns
            .GroupBy(sr => sr.SyncType)
            .ToDictionary(g => g.Key, g => g.Count());

        statistics.RunsByTriggerSource = syncRuns
            .GroupBy(sr => sr.TriggerSource)
            .ToDictionary(g => g.Key, g => g.Count());

        // Extract common errors (simplified - would need more sophisticated error analysis)
        statistics.CommonErrors = syncRuns
            .Where(sr => !string.IsNullOrEmpty(sr.ErrorsJson))
            .SelectMany(sr => ExtractErrorMessages(sr.ErrorsJson!))
            .GroupBy(error => error)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        return statistics;
    }

    public async Task<List<MenuSyncRun>> GetLongRunningSyncRunsAsync(
        TimeSpan threshold,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var cutoffTime = DateTime.UtcNow - threshold;

        return await dbSet
            .Where(sr => sr.Status == MenuSyncRunStatus.Running)
            .Where(sr => sr.StartedAt < cutoffTime)
            .OrderBy(sr => sr.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MenuSyncRun>> GetHighErrorRateSyncRunsAsync(
        double errorRateThreshold = 0.1,
        DateTime? fromDate = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var cutoffDate = fromDate ?? DateTime.UtcNow.AddDays(-7);

        var syncRuns = await dbSet
            .Where(sr => sr.StartedAt >= cutoffDate)
            .Where(sr => sr.TotalProductsProcessed > 0)
            .ToListAsync(cancellationToken);

        return syncRuns
            .Where(sr => (double)sr.ProductsFailed / sr.TotalProductsProcessed > errorRateThreshold)
            .OrderByDescending(sr => (double)sr.ProductsFailed / sr.TotalProductsProcessed)
            .ToList();
    }

    public async Task<int> CleanupOldSyncRunsAsync(
        int retentionDays = 30,
        bool keepFailedRuns = true,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        var query = dbSet.Where(sr => sr.StartedAt < cutoffDate);

        if (keepFailedRuns)
        {
            query = query.Where(sr => sr.Status == MenuSyncRunStatus.Completed);
        }
        else
        {
            query = query.Where(sr => sr.Status == MenuSyncRunStatus.Completed || 
                                     sr.Status == MenuSyncRunStatus.Failed);
        }

        var oldSyncRuns = await query.ToListAsync(cancellationToken);
        
        if (oldSyncRuns.Any())
        {
            dbSet.RemoveRange(oldSyncRuns);
            await (await GetDbContextAsync()).SaveChangesAsync(cancellationToken);
        }

        return oldSyncRuns.Count;
    }

    private static List<string> ExtractErrorMessages(string errorsJson)
    {
        try
        {
            var errors = System.Text.Json.JsonSerializer.Deserialize<List<string>>(errorsJson);
            return errors ?? new List<string>();
        }
        catch
        {
            return new List<string> { "Error parsing failed" };
        }
    }
}