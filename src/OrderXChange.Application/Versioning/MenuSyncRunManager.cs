using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Manager for MenuSyncRun lifecycle with comprehensive logging and tracing
/// Provides centralized orchestration of sync operations with full observability
/// </summary>
public class MenuSyncRunManager : ITransientDependency
{
    private readonly IMenuSyncRunRepository _syncRunRepository;
    private readonly ILogger<MenuSyncRunManager> _logger;
    private readonly Dictionary<Guid, SyncRunTracer> _activeTracers = new();

    // Public accessor for repository (needed by MenuSyncOrchestrator)
    public IMenuSyncRunRepository SyncRunRepository => _syncRunRepository;

    public MenuSyncRunManager(
        IMenuSyncRunRepository syncRunRepository,
        ILogger<MenuSyncRunManager> logger)
    {
        _syncRunRepository = syncRunRepository;
        _logger = logger;
    }

    /// <summary>
    /// Creates and starts a new sync run with full tracing
    /// </summary>
    public async Task<MenuSyncRun> StartSyncRunAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId,
        string syncType,
        string triggerSource,
        string initiatedBy,
        Dictionary<string, object>? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = GenerateCorrelationId();
        
        var syncRun = new MenuSyncRun
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            MenuGroupId = menuGroupId,
            CorrelationId = correlationId,
            SyncType = syncType,
            TriggerSource = triggerSource,
            ConfigurationJson = configuration != null ? JsonSerializer.Serialize(configuration) : null
        };

        // Start the sync run
        syncRun.Start(initiatedBy, MenuSyncPhase.Initialization);

        // Create tracer for detailed logging
        var tracer = new SyncRunTracer(syncRun.Id, correlationId, _logger);
        _activeTracers[syncRun.Id] = tracer;

        // Log structured start event
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SyncRunId"] = syncRun.Id,
            ["CorrelationId"] = correlationId,
            ["FoodicsAccountId"] = foodicsAccountId,
            ["BranchId"] = branchId ?? "ALL",
            ["SyncType"] = syncType,
            ["TriggerSource"] = triggerSource
        });

        _logger.LogInformation(
            "MenuSyncRun started. SyncRunId={SyncRunId}, CorrelationId={CorrelationId}, " +
            "Account={FoodicsAccountId}, Branch={BranchId}, Type={SyncType}, Trigger={TriggerSource}",
            syncRun.Id, correlationId, foodicsAccountId, branchId ?? "ALL", syncType, triggerSource);

        tracer.RecordEvent("SyncRunStarted", new
        {
            syncRun.Id,
            syncRun.CorrelationId,
            syncRun.FoodicsAccountId,
            syncRun.BranchId,
            syncRun.SyncType,
            syncRun.TriggerSource,
            syncRun.InitiatedBy,
            Configuration = configuration
        });

        await _syncRunRepository.InsertAsync(syncRun, autoSave: true, cancellationToken: cancellationToken);

        return syncRun;
    }

    /// <summary>
    /// Updates sync run progress with detailed tracing
    /// </summary>
    public async Task UpdateProgressAsync(
        Guid syncRunId,
        string phase,
        int progressPercentage,
        string? details = null,
        Dictionary<string, object>? metrics = null,
        CancellationToken cancellationToken = default)
    {
        var syncRun = await _syncRunRepository.GetAsync(syncRunId, cancellationToken: cancellationToken);
        var tracer = GetTracer(syncRunId);

        syncRun.UpdateProgress(phase, progressPercentage, details);

        if (metrics != null)
        {
            syncRun.MetricsJson = JsonSerializer.Serialize(metrics);
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SyncRunId"] = syncRunId,
            ["CorrelationId"] = syncRun.CorrelationId,
            ["Phase"] = phase,
            ["Progress"] = progressPercentage
        });

        _logger.LogInformation(
            "MenuSyncRun progress updated. Phase={Phase}, Progress={Progress}%, Details={Details}",
            phase, progressPercentage, details);

        tracer?.RecordEvent("ProgressUpdated", new
        {
            Phase = phase,
            ProgressPercentage = progressPercentage,
            Details = details,
            Metrics = metrics,
            Timestamp = DateTime.UtcNow
        });

        await _syncRunRepository.UpdateAsync(syncRun, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Completes sync run with comprehensive result logging
    /// </summary>
    public async Task CompleteSyncRunAsync(
        Guid syncRunId,
        string result = "Success",
        Dictionary<string, object>? finalMetrics = null,
        CancellationToken cancellationToken = default)
    {
        var syncRun = await _syncRunRepository.GetAsync(syncRunId, cancellationToken: cancellationToken);
        var tracer = GetTracer(syncRunId);

        syncRun.Complete(result);

        if (finalMetrics != null)
        {
            syncRun.MetricsJson = JsonSerializer.Serialize(finalMetrics);
        }

        // Store compressed trace data
        if (tracer != null)
        {
            syncRun.CompressedTraceData = await tracer.GetCompressedTraceDataAsync();
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SyncRunId"] = syncRunId,
            ["CorrelationId"] = syncRun.CorrelationId,
            ["Result"] = result,
            ["Duration"] = syncRun.Duration?.TotalSeconds ?? 0,
            ["ProductsProcessed"] = syncRun.TotalProductsProcessed,
            ["SuccessRate"] = syncRun.SuccessRate
        });

        _logger.LogInformation(
            "MenuSyncRun completed. Result={Result}, Duration={Duration}s, " +
            "Processed={ProductsProcessed}, SuccessRate={SuccessRate}%",
            result, syncRun.Duration?.TotalSeconds ?? 0, syncRun.TotalProductsProcessed, syncRun.SuccessRate);

        tracer?.RecordEvent("SyncRunCompleted", new
        {
            Result = result,
            syncRun.Duration,
            syncRun.TotalProductsProcessed,
            syncRun.ProductsSucceeded,
            syncRun.ProductsFailed,
            syncRun.SuccessRate,
            FinalMetrics = finalMetrics
        });

        await _syncRunRepository.UpdateAsync(syncRun, autoSave: true, cancellationToken: cancellationToken);

        // Cleanup tracer
        _activeTracers.Remove(syncRunId);
    }

    /// <summary>
    /// Fails sync run with detailed error logging
    /// </summary>
    public async Task FailSyncRunAsync(
        Guid syncRunId,
        string error,
        Exception? exception = null,
        Dictionary<string, object>? errorContext = null,
        CancellationToken cancellationToken = default)
    {
        var syncRun = await _syncRunRepository.GetAsync(syncRunId, cancellationToken: cancellationToken);
        var tracer = GetTracer(syncRunId);

        syncRun.Fail(error, exception);

        // Store compressed trace data for debugging
        if (tracer != null)
        {
            syncRun.CompressedTraceData = await tracer.GetCompressedTraceDataAsync();
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SyncRunId"] = syncRunId,
            ["CorrelationId"] = syncRun.CorrelationId,
            ["Error"] = error,
            ["Phase"] = syncRun.CurrentPhase ?? "Unknown"
        });

        _logger.LogError(exception,
            "MenuSyncRun failed. Error={Error}, Phase={Phase}, Duration={Duration}s",
            error, syncRun.CurrentPhase, syncRun.Duration?.TotalSeconds ?? 0);

        tracer?.RecordEvent("SyncRunFailed", new
        {
            Error = error,
            Exception = exception?.ToString(),
            syncRun.CurrentPhase,
            syncRun.Duration,
            ErrorContext = errorContext
        });

        await _syncRunRepository.UpdateAsync(syncRun, autoSave: true, cancellationToken: cancellationToken);

        // Cleanup tracer
        _activeTracers.Remove(syncRunId);
    }

    /// <summary>
    /// Adds error to sync run with contextual information
    /// </summary>
    public async Task AddErrorAsync(
        Guid syncRunId,
        string error,
        Exception? exception = null,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var syncRun = await _syncRunRepository.GetAsync(syncRunId, cancellationToken: cancellationToken);
        var tracer = GetTracer(syncRunId);

        syncRun.AddError(error, exception);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SyncRunId"] = syncRunId,
            ["CorrelationId"] = syncRun.CorrelationId,
            ["Phase"] = syncRun.CurrentPhase ?? "Unknown"
        });

        _logger.LogWarning(exception, "MenuSyncRun error. Error={Error}, Phase={Phase}", error, syncRun.CurrentPhase);

        tracer?.RecordEvent("ErrorAdded", new
        {
            Error = error,
            Exception = exception?.ToString(),
            syncRun.CurrentPhase,
            Context = context
        });

        await _syncRunRepository.UpdateAsync(syncRun, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Adds warning to sync run
    /// </summary>
    public async Task AddWarningAsync(
        Guid syncRunId,
        string warning,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var syncRun = await _syncRunRepository.GetAsync(syncRunId, cancellationToken: cancellationToken);
        var tracer = GetTracer(syncRunId);

        syncRun.AddWarning(warning);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SyncRunId"] = syncRunId,
            ["CorrelationId"] = syncRun.CorrelationId
        });

        _logger.LogWarning("MenuSyncRun warning. Warning={Warning}, Phase={Phase}", warning, syncRun.CurrentPhase);

        tracer?.RecordEvent("WarningAdded", new
        {
            Warning = warning,
            syncRun.CurrentPhase,
            Context = context
        });

        await _syncRunRepository.UpdateAsync(syncRun, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates sync statistics
    /// </summary>
    public async Task UpdateStatisticsAsync(
        Guid syncRunId,
        int totalProcessed = 0,
        int succeeded = 0,
        int failed = 0,
        int skipped = 0,
        int added = 0,
        int updated = 0,
        int deleted = 0,
        CancellationToken cancellationToken = default)
    {
        var syncRun = await _syncRunRepository.GetAsync(syncRunId, cancellationToken: cancellationToken);
        var tracer = GetTracer(syncRunId);

        syncRun.UpdateStatistics(totalProcessed, succeeded, failed, skipped, added, updated, deleted);

        tracer?.RecordEvent("StatisticsUpdated", new
        {
            TotalProcessed = totalProcessed,
            Succeeded = succeeded,
            Failed = failed,
            Skipped = skipped,
            Added = added,
            Updated = updated,
            Deleted = deleted,
            SuccessRate = syncRun.SuccessRate
        });

        await _syncRunRepository.UpdateAsync(syncRun, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sets Talabat sync information
    /// </summary>
    public async Task SetTalabatSyncInfoAsync(
        Guid syncRunId,
        string vendorCode,
        string? importId = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var syncRun = await _syncRunRepository.GetAsync(syncRunId, cancellationToken: cancellationToken);
        var tracer = GetTracer(syncRunId);

        syncRun.SetTalabatSyncInfo(vendorCode, importId, status);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SyncRunId"] = syncRunId,
            ["CorrelationId"] = syncRun.CorrelationId,
            ["TalabatVendorCode"] = vendorCode
        });

        _logger.LogInformation(
            "Talabat sync info updated. VendorCode={VendorCode}, ImportId={ImportId}, Status={Status}",
            vendorCode, importId, status);

        tracer?.RecordEvent("TalabatSyncInfoUpdated", new
        {
            VendorCode = vendorCode,
            ImportId = importId,
            Status = status
        });

        await _syncRunRepository.UpdateAsync(syncRun, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets active sync runs for monitoring
    /// </summary>
    public async Task<List<MenuSyncRun>> GetActiveSyncRunsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        return await _syncRunRepository.GetActiveSyncRunsAsync(foodicsAccountId, branchId, cancellationToken);
    }

    /// <summary>
    /// Gets sync run statistics for reporting
    /// </summary>
    public async Task<MenuSyncRunStatistics> GetStatisticsAsync(
        Guid foodicsAccountId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        return await _syncRunRepository.GetSyncRunStatisticsAsync(foodicsAccountId, fromDate, toDate, cancellationToken);
    }

    private SyncRunTracer? GetTracer(Guid syncRunId)
    {
        _activeTracers.TryGetValue(syncRunId, out var tracer);
        return tracer;
    }

    private static string GenerateCorrelationId()
    {
        return $"sync_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
    }
}

/// <summary>
/// Detailed tracer for sync run operations
/// Captures fine-grained execution details for debugging and analysis
/// </summary>
public class SyncRunTracer
{
    private readonly Guid _syncRunId;
    private readonly string _correlationId;
    private readonly ILogger _logger;
    private readonly List<TraceEvent> _events = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public SyncRunTracer(Guid syncRunId, string correlationId, ILogger logger)
    {
        _syncRunId = syncRunId;
        _correlationId = correlationId;
        _logger = logger;
    }

    public void RecordEvent(string eventType, object? data = null)
    {
        var traceEvent = new TraceEvent
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            ElapsedMilliseconds = _stopwatch.ElapsedMilliseconds,
            Data = data != null ? JsonSerializer.Serialize(data) : null
        };

        _events.Add(traceEvent);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["SyncRunId"] = _syncRunId,
            ["CorrelationId"] = _correlationId,
            ["EventType"] = eventType,
            ["ElapsedMs"] = _stopwatch.ElapsedMilliseconds
        });

        _logger.LogDebug("Trace event: {EventType} at {ElapsedMs}ms", eventType, _stopwatch.ElapsedMilliseconds);
    }

    public async Task<byte[]> GetCompressedTraceDataAsync()
    {
        var traceData = new
        {
            SyncRunId = _syncRunId,
            CorrelationId = _correlationId,
            TotalDuration = _stopwatch.Elapsed,
            Events = _events
        };

        var json = JsonSerializer.Serialize(traceData, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);

        using var memoryStream = new System.IO.MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
        {
            await gzipStream.WriteAsync(bytes, 0, bytes.Length);
        }

        return memoryStream.ToArray();
    }

    private class TraceEvent
    {
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string? Data { get; set; }
    }
}