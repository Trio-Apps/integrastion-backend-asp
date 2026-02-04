using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Versioning.DTOs;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Real-time performance monitoring for menu sync operations
/// Tracks metrics, identifies bottlenecks, and provides optimization insights
/// </summary>
public class MenuSyncPerformanceMonitor : ITransientDependency
{
    private readonly ILogger<MenuSyncPerformanceMonitor> _logger;
    private readonly ConcurrentDictionary<string, PerformanceTracker> _trackers = new();
    private readonly ConcurrentQueue<MenuSyncPerformanceMetrics> _metricsHistory = new();
    private readonly Timer _cleanupTimer;

    public MenuSyncPerformanceMonitor(ILogger<MenuSyncPerformanceMonitor> logger)
    {
        _logger = logger;
        
        // Cleanup old metrics every 5 minutes
        _cleanupTimer = new Timer(CleanupOldMetrics, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Starts tracking performance for a menu sync operation
    /// </summary>
    public IDisposable StartTracking(string operationName, Guid foodicsAccountId, Dictionary<string, object>? context = null)
    {
        var trackerId = $"{operationName}_{Guid.NewGuid():N}";
        var tracker = new PerformanceTracker
        {
            Id = trackerId,
            OperationName = operationName,
            FoodicsAccountId = foodicsAccountId,
            StartTime = DateTime.UtcNow,
            Context = context ?? new Dictionary<string, object>(),
            Stopwatch = Stopwatch.StartNew()
        };

        _trackers[trackerId] = tracker;

        _logger.LogDebug("Started performance tracking. Operation={Operation}, TrackerId={TrackerId}", 
            operationName, trackerId);

        return new PerformanceTrackingScope(this, trackerId);
    }

    /// <summary>
    /// Records a performance metric during operation execution
    /// </summary>
    public void RecordMetric(string trackerId, string metricName, double value, string? unit = null)
    {
        if (_trackers.TryGetValue(trackerId, out var tracker))
        {
            tracker.Metrics[metricName] = new MetricValue
            {
                Value = value,
                Unit = unit ?? "",
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace("Recorded metric. TrackerId={TrackerId}, Metric={Metric}, Value={Value}", 
                trackerId, metricName, value);
        }
    }

    /// <summary>
    /// Records throughput metrics for batch operations
    /// </summary>
    public void RecordThroughput(string trackerId, int itemsProcessed, TimeSpan duration)
    {
        var throughput = itemsProcessed / duration.TotalSeconds;
        RecordMetric(trackerId, "throughput", throughput, "items/sec");
        RecordMetric(trackerId, "items_processed", itemsProcessed, "items");
        RecordMetric(trackerId, "duration", duration.TotalMilliseconds, "ms");
    }

    /// <summary>
    /// Records error information for failed operations
    /// </summary>
    public void RecordError(string trackerId, Exception exception, string? context = null)
    {
        if (_trackers.TryGetValue(trackerId, out var tracker))
        {
            tracker.Errors.Add(new ErrorInfo
            {
                Exception = exception,
                Context = context,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogWarning(exception, "Recorded error. TrackerId={TrackerId}, Context={Context}", 
                trackerId, context);
        }
    }

    /// <summary>
    /// Completes performance tracking and generates metrics
    /// </summary>
    public MenuSyncPerformanceMetrics CompleteTracking(string trackerId)
    {
        if (!_trackers.TryRemove(trackerId, out var tracker))
        {
            _logger.LogWarning("Tracker not found. TrackerId={TrackerId}", trackerId);
            return new MenuSyncPerformanceMetrics();
        }

        tracker.Stopwatch.Stop();
        tracker.EndTime = DateTime.UtcNow;

        var metrics = GenerateMetrics(tracker);
        _metricsHistory.Enqueue(metrics);

        // Keep only recent metrics
        while (_metricsHistory.Count > 1000)
        {
            _metricsHistory.TryDequeue(out _);
        }

        _logger.LogInformation(
            "Completed performance tracking. Operation={Operation}, Duration={Duration}ms, Throughput={Throughput:F1}/sec",
            tracker.OperationName, tracker.Stopwatch.ElapsedMilliseconds, metrics.ItemsPerSecond);

        return metrics;
    }

    /// <summary>
    /// Gets performance metrics for a specific account and time range
    /// </summary>
    public List<MenuSyncPerformanceMetrics> GetMetrics(
        Guid foodicsAccountId,
        DateTime fromDate,
        DateTime toDate,
        string? operationFilter = null)
    {
        return _metricsHistory
            .Where(m => m.FoodicsAccountId == foodicsAccountId)
            .Where(m => m.Timestamp >= fromDate && m.Timestamp <= toDate)
            .Where(m => operationFilter == null || m.Operation == operationFilter)
            .OrderByDescending(m => m.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Analyzes performance trends and identifies bottlenecks
    /// </summary>
    public PerformanceAnalysisResult AnalyzePerformance(
        Guid foodicsAccountId,
        TimeSpan analysisWindow)
    {
        var fromDate = DateTime.UtcNow - analysisWindow;
        var metrics = GetMetrics(foodicsAccountId, fromDate, DateTime.UtcNow);

        if (!metrics.Any())
        {
            return new PerformanceAnalysisResult
            {
                FoodicsAccountId = foodicsAccountId,
                AnalysisWindow = analysisWindow,
                HasSufficientData = false
            };
        }

        var result = new PerformanceAnalysisResult
        {
            FoodicsAccountId = foodicsAccountId,
            AnalysisWindow = analysisWindow,
            HasSufficientData = true,
            TotalOperations = metrics.Count
        };

        // Calculate averages
        result.AverageThroughput = metrics.Average(m => m.ItemsPerSecond);
        result.AverageLatency = TimeSpan.FromMilliseconds(metrics.Average(m => m.AverageLatency.TotalMilliseconds));
        result.AverageErrorRate = metrics.Average(m => m.ErrorRate);

        // Calculate percentiles
        var latencies = metrics.Select(m => m.AverageLatency.TotalMilliseconds).OrderBy(x => x).ToList();
        if (latencies.Any())
        {
            result.P95Latency = TimeSpan.FromMilliseconds(GetPercentile(latencies, 0.95));
            result.P99Latency = TimeSpan.FromMilliseconds(GetPercentile(latencies, 0.99));
        }

        // Identify trends
        result.ThroughputTrend = CalculateTrend(metrics.Select(m => m.ItemsPerSecond).ToList());
        result.LatencyTrend = CalculateTrend(metrics.Select(m => m.AverageLatency.TotalMilliseconds).ToList());
        result.ErrorRateTrend = CalculateTrend(metrics.Select(m => m.ErrorRate).ToList());

        // Generate recommendations
        result.Recommendations = GenerateRecommendations(result, metrics);

        return result;
    }

    /// <summary>
    /// Gets real-time performance dashboard data
    /// </summary>
    public PerformanceDashboard GetDashboard(Guid foodicsAccountId)
    {
        var recentMetrics = GetMetrics(foodicsAccountId, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        var activeTrackers = _trackers.Values.Where(t => t.FoodicsAccountId == foodicsAccountId).ToList();

        return new PerformanceDashboard
        {
            FoodicsAccountId = foodicsAccountId,
            LastUpdated = DateTime.UtcNow,
            ActiveOperations = activeTrackers.Count,
            RecentOperations = recentMetrics.Count,
            CurrentThroughput = recentMetrics.Any() ? recentMetrics.Average(m => m.ItemsPerSecond) : 0,
            CurrentErrorRate = recentMetrics.Any() ? recentMetrics.Average(m => m.ErrorRate) : 0,
            AverageLatency = recentMetrics.Any() 
                ? TimeSpan.FromMilliseconds(recentMetrics.Average(m => m.AverageLatency.TotalMilliseconds))
                : TimeSpan.Zero,
            OperationBreakdown = recentMetrics
                .GroupBy(m => m.Operation)
                .ToDictionary(g => g.Key, g => g.Count()),
            ThroughputHistory = recentMetrics
                .OrderBy(m => m.Timestamp)
                .Select(m => new { m.Timestamp, m.ItemsPerSecond })
                .ToList()
        };
    }

    #region Private Methods

    private MenuSyncPerformanceMetrics GenerateMetrics(PerformanceTracker tracker)
    {
        var metrics = new MenuSyncPerformanceMetrics
        {
            Timestamp = tracker.EndTime ?? DateTime.UtcNow,
            FoodicsAccountId = tracker.FoodicsAccountId,
            Operation = tracker.OperationName
        };

        // Extract throughput metrics
        if (tracker.Metrics.TryGetValue("throughput", out var throughput))
            metrics.ItemsPerSecond = throughput.Value;

        if (tracker.Metrics.TryGetValue("items_processed", out var items))
            metrics.RequestsPerSecond = items.Value / tracker.Stopwatch.Elapsed.TotalSeconds;

        // Calculate latency
        metrics.AverageLatency = tracker.Stopwatch.Elapsed;

        // Extract resource utilization if available
        if (tracker.Metrics.TryGetValue("cpu_usage", out var cpu))
            metrics.CpuUtilization = cpu.Value;

        if (tracker.Metrics.TryGetValue("memory_usage", out var memory))
            metrics.MemoryUsageBytes = (long)memory.Value;

        // Calculate error rate
        var totalOperations = tracker.Metrics.TryGetValue("total_operations", out var total) ? total.Value : 1;
        metrics.ErrorRate = tracker.Errors.Count / totalOperations * 100;

        // Extract compression ratio if available
        if (tracker.Metrics.TryGetValue("compression_ratio", out var compression))
            metrics.CompressionRatio = compression.Value;

        return metrics;
    }

    private double GetPercentile(List<double> values, double percentile)
    {
        if (!values.Any()) return 0;
        
        var index = (int)Math.Ceiling(values.Count * percentile) - 1;
        return values[Math.Max(0, Math.Min(index, values.Count - 1))];
    }

    private TrendDirection CalculateTrend(List<double> values)
    {
        if (values.Count < 2) return TrendDirection.Stable;

        var firstHalf = values.Take(values.Count / 2).Average();
        var secondHalf = values.Skip(values.Count / 2).Average();

        var change = (secondHalf - firstHalf) / firstHalf * 100;

        return change switch
        {
            > 10 => TrendDirection.Increasing,
            < -10 => TrendDirection.Decreasing,
            _ => TrendDirection.Stable
        };
    }

    private List<string> GenerateRecommendations(PerformanceAnalysisResult analysis, List<MenuSyncPerformanceMetrics> metrics)
    {
        var recommendations = new List<string>();

        if (analysis.AverageThroughput < 5)
        {
            recommendations.Add("Low throughput detected - consider increasing batch sizes or concurrency");
        }

        if (analysis.AverageLatency > TimeSpan.FromSeconds(30))
        {
            recommendations.Add("High latency detected - optimize payload sizes or enable compression");
        }

        if (analysis.AverageErrorRate > 5)
        {
            recommendations.Add("High error rate detected - review retry policies and error handling");
        }

        if (analysis.ThroughputTrend == TrendDirection.Decreasing)
        {
            recommendations.Add("Throughput is decreasing - investigate system resource constraints");
        }

        if (analysis.LatencyTrend == TrendDirection.Increasing)
        {
            recommendations.Add("Latency is increasing - consider scaling resources or optimizing queries");
        }

        return recommendations;
    }

    private void CleanupOldMetrics(object? state)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var activeTrackers = _trackers.Values.Where(t => t.StartTime < cutoff).ToList();
            
            foreach (var tracker in activeTrackers)
            {
                _trackers.TryRemove(tracker.Id, out _);
            }

            _logger.LogDebug("Cleaned up {Count} old performance trackers", activeTrackers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old performance metrics");
        }
    }

    #endregion

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}

/// <summary>
/// Performance tracking scope that automatically completes tracking when disposed
/// </summary>
public class PerformanceTrackingScope : IDisposable
{
    private readonly MenuSyncPerformanceMonitor _monitor;
    private readonly string _trackerId;
    private bool _disposed;

    public PerformanceTrackingScope(MenuSyncPerformanceMonitor monitor, string trackerId)
    {
        _monitor = monitor;
        _trackerId = trackerId;
    }

    public void RecordMetric(string metricName, double value, string? unit = null)
    {
        _monitor.RecordMetric(_trackerId, metricName, value, unit);
    }

    public void RecordThroughput(int itemsProcessed, TimeSpan duration)
    {
        _monitor.RecordThroughput(_trackerId, itemsProcessed, duration);
    }

    public void RecordError(Exception exception, string? context = null)
    {
        _monitor.RecordError(_trackerId, exception, context);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _monitor.CompleteTracking(_trackerId);
            _disposed = true;
        }
    }
}

/// <summary>
/// Internal performance tracker
/// </summary>
internal class PerformanceTracker
{
    public string Id { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public Guid FoodicsAccountId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public Stopwatch Stopwatch { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
    public Dictionary<string, MetricValue> Metrics { get; set; } = new();
    public List<ErrorInfo> Errors { get; set; } = new();
}

/// <summary>
/// Metric value with metadata
/// </summary>
internal class MetricValue
{
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Error information
/// </summary>
internal class ErrorInfo
{
    public Exception Exception { get; set; } = null!;
    public string? Context { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Performance analysis result
/// </summary>
public class PerformanceAnalysisResult
{
    public Guid FoodicsAccountId { get; set; }
    public TimeSpan AnalysisWindow { get; set; }
    public bool HasSufficientData { get; set; }
    public int TotalOperations { get; set; }
    public double AverageThroughput { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public TimeSpan P95Latency { get; set; }
    public TimeSpan P99Latency { get; set; }
    public double AverageErrorRate { get; set; }
    public TrendDirection ThroughputTrend { get; set; }
    public TrendDirection LatencyTrend { get; set; }
    public TrendDirection ErrorRateTrend { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Performance dashboard data
/// </summary>
public class PerformanceDashboard
{
    public Guid FoodicsAccountId { get; set; }
    public DateTime LastUpdated { get; set; }
    public int ActiveOperations { get; set; }
    public int RecentOperations { get; set; }
    public double CurrentThroughput { get; set; }
    public double CurrentErrorRate { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public Dictionary<string, int> OperationBreakdown { get; set; } = new();
    public object ThroughputHistory { get; set; } = new();
}

/// <summary>
/// Trend direction enumeration
/// </summary>
public enum TrendDirection
{
    Decreasing,
    Stable,
    Increasing
}