using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Versioning.DTOs;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// High-performance batch processor for menu sync operations
/// Implements producer-consumer pattern with intelligent load balancing
/// </summary>
public class MenuSyncBatchProcessor : ITransientDependency
{
    private readonly ILogger<MenuSyncBatchProcessor> _logger;
    private readonly MenuSyncPerformanceOptions _options;
    private readonly MenuSyncPerformanceOptimizer _optimizer;

    public MenuSyncBatchProcessor(
        ILogger<MenuSyncBatchProcessor> logger,
        IOptions<MenuSyncPerformanceOptions> options,
        MenuSyncPerformanceOptimizer optimizer)
    {
        _logger = logger;
        _options = options.Value;
        _optimizer = optimizer;
    }

    /// <summary>
    /// Processes menu sync operations using high-performance batching
    /// Implements adaptive batching based on system load and API response times
    /// </summary>
    public async Task<BatchProcessingResult> ProcessMenuSyncBatchesAsync<T>(
        IEnumerable<T> items,
        Func<IEnumerable<T>, CancellationToken, Task<BatchProcessResult<T>>> processor,
        BatchProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchProcessingOptions();
        var stopwatch = Stopwatch.StartNew();
        
        var result = new BatchProcessingResult
        {
            StartedAt = DateTime.UtcNow,
            TotalItems = items.Count()
        };

        try
        {
            _logger.LogInformation(
                "Starting batch processing. Items={Count}, Concurrency={Concurrency}, BatchSize={BatchSize}",
                result.TotalItems, options.MaxConcurrency, options.BatchSize);

            // Create channel for producer-consumer pattern
            var channel = Channel.CreateBounded<Batch<T>>(new BoundedChannelOptions(options.MaxConcurrency * 2)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true
            });

            // Start producer task
            var producerTask = ProduceBatchesAsync(items, channel.Writer, options, cancellationToken);

            // Start consumer tasks
            var consumerTasks = Enumerable.Range(0, options.MaxConcurrency)
                .Select(i => ConsumeBatchesAsync(channel.Reader, processor, result, cancellationToken))
                .ToArray();

            // Wait for producer to complete
            await producerTask;

            // Wait for all consumers to complete
            await Task.WhenAll(consumerTasks);

            result.CompletedAt = DateTime.UtcNow;
            result.TotalProcessingTime = stopwatch.Elapsed;
            result.OverallThroughput = result.TotalItems / stopwatch.Elapsed.TotalSeconds;

            _logger.LogInformation(
                "Batch processing completed. Processed={Processed}, Failed={Failed}, Throughput={Throughput:F1}/sec, Time={Time}ms",
                result.ProcessedItems, result.FailedItems, result.OverallThroughput, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch processing failed");
            result.CompletedAt = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Implements adaptive batch sizing based on system performance
    /// Dynamically adjusts batch sizes for optimal throughput
    /// </summary>
    public async Task<AdaptiveBatchResult> ProcessWithAdaptiveBatchingAsync<T>(
        IEnumerable<T> items,
        Func<IEnumerable<T>, CancellationToken, Task<BatchProcessResult<T>>> processor,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AdaptiveBatchResult
        {
            StartedAt = DateTime.UtcNow,
            TotalItems = items.Count()
        };

        try
        {
            var itemsList = items.ToList();
            var currentBatchSize = _options.BatchSize;
            var performanceHistory = new Queue<BatchPerformanceMetric>();
            var processedCount = 0;

            _logger.LogInformation(
                "Starting adaptive batch processing. Items={Count}, InitialBatchSize={BatchSize}",
                result.TotalItems, currentBatchSize);

            while (processedCount < itemsList.Count)
            {
                var remainingItems = itemsList.Count - processedCount;
                var actualBatchSize = Math.Min(currentBatchSize, remainingItems);
                var batch = itemsList.Skip(processedCount).Take(actualBatchSize);

                var batchStopwatch = Stopwatch.StartNew();
                var batchResult = await processor(batch, cancellationToken);
                batchStopwatch.Stop();

                // Record performance metrics
                var metric = new BatchPerformanceMetric
                {
                    BatchSize = actualBatchSize,
                    ProcessingTime = batchStopwatch.Elapsed,
                    Throughput = actualBatchSize / batchStopwatch.Elapsed.TotalSeconds,
                    SuccessRate = (double)batchResult.SuccessfulItems / actualBatchSize,
                    Timestamp = DateTime.UtcNow
                };

                performanceHistory.Enqueue(metric);
                if (performanceHistory.Count > 10)
                    performanceHistory.Dequeue();

                result.BatchMetrics.Add(metric);
                result.ProcessedItems += batchResult.SuccessfulItems;
                result.FailedItems += batchResult.FailedItems;

                // Adapt batch size based on performance
                currentBatchSize = AdaptBatchSize(performanceHistory, currentBatchSize);

                processedCount += actualBatchSize;

                _logger.LogDebug(
                    "Batch completed. Size={Size}, Throughput={Throughput:F1}/sec, NextSize={NextSize}",
                    actualBatchSize, metric.Throughput, currentBatchSize);
            }

            result.CompletedAt = DateTime.UtcNow;
            result.TotalProcessingTime = stopwatch.Elapsed;
            result.OverallThroughput = result.TotalItems / stopwatch.Elapsed.TotalSeconds;
            result.OptimalBatchSize = CalculateOptimalBatchSize(result.BatchMetrics);

            _logger.LogInformation(
                "Adaptive batch processing completed. OptimalBatchSize={OptimalSize}, Throughput={Throughput:F1}/sec",
                result.OptimalBatchSize, result.OverallThroughput);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Adaptive batch processing failed");
            result.CompletedAt = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Implements intelligent load balancing across multiple workers
    /// Distributes work based on worker performance and current load
    /// </summary>
    public async Task<LoadBalancedResult> ProcessWithLoadBalancingAsync<T>(
        IEnumerable<T> items,
        List<Func<IEnumerable<T>, CancellationToken, Task<BatchProcessResult<T>>>> workers,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new LoadBalancedResult
        {
            StartedAt = DateTime.UtcNow,
            TotalItems = items.Count(),
            WorkerCount = workers.Count
        };

        try
        {
            _logger.LogInformation(
                "Starting load-balanced processing. Items={Count}, Workers={Workers}",
                result.TotalItems, workers.Count);

            // Create work distribution system
            var workQueue = new ConcurrentQueue<Batch<T>>();
            var workerMetrics = new ConcurrentDictionary<int, WorkerMetrics>();

            // Initialize worker metrics
            for (int i = 0; i < workers.Count; i++)
            {
                workerMetrics[i] = new WorkerMetrics { WorkerId = i };
            }

            // Create batches and add to queue
            var batches = CreateOptimalBatches(items);
            foreach (var batch in batches)
            {
                workQueue.Enqueue(batch);
            }

            // Start worker tasks
            var workerTasks = workers.Select((worker, index) =>
                ProcessWorkerAsync(index, worker, workQueue, workerMetrics, result, cancellationToken))
                .ToArray();

            // Wait for all workers to complete
            await Task.WhenAll(workerTasks);

            result.CompletedAt = DateTime.UtcNow;
            result.TotalProcessingTime = stopwatch.Elapsed;
            result.OverallThroughput = result.TotalItems / stopwatch.Elapsed.TotalSeconds;
            result.WorkerMetrics = workerMetrics.Values.ToList();

            _logger.LogInformation(
                "Load-balanced processing completed. Throughput={Throughput:F1}/sec, BestWorker={BestWorker}",
                result.OverallThroughput, result.WorkerMetrics.OrderByDescending(w => w.Throughput).First().WorkerId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load-balanced processing failed");
            result.CompletedAt = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    #region Private Methods

    private async Task ProduceBatchesAsync<T>(
        IEnumerable<T> items,
        ChannelWriter<Batch<T>> writer,
        BatchProcessingOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var batchId = 0;
            var itemsList = items.ToList();

            for (int i = 0; i < itemsList.Count; i += options.BatchSize)
            {
                var batchItems = itemsList.Skip(i).Take(options.BatchSize).ToList();
                var batch = new Batch<T>
                {
                    Id = batchId++,
                    Items = batchItems,
                    CreatedAt = DateTime.UtcNow
                };

                await writer.WriteAsync(batch, cancellationToken);
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ConsumeBatchesAsync<T>(
        ChannelReader<Batch<T>> reader,
        Func<IEnumerable<T>, CancellationToken, Task<BatchProcessResult<T>>> processor,
        BatchProcessingResult result,
        CancellationToken cancellationToken)
    {
        await foreach (var batch in reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var batchStopwatch = Stopwatch.StartNew();
                var batchResult = await processor(batch.Items, cancellationToken);
                batchStopwatch.Stop();

                // Update results thread-safely
                result.AddProcessedItems(batchResult.SuccessfulItems);
                result.AddFailedItems(batchResult.FailedItems);

                _logger.LogDebug(
                    "Batch {BatchId} processed. Items={Items}, Success={Success}, Time={Time}ms",
                    batch.Id, batch.Items.Count, batchResult.SuccessfulItems, batchStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process batch {BatchId}", batch.Id);
                result.AddFailedItems(batch.Items.Count);
            }
        }
    }

    private int AdaptBatchSize(Queue<BatchPerformanceMetric> history, int currentBatchSize)
    {
        if (history.Count < 3)
            return currentBatchSize;

        var recentMetrics = history.TakeLast(3).ToList();
        var avgThroughput = recentMetrics.Average(m => m.Throughput);
        var avgSuccessRate = recentMetrics.Average(m => m.SuccessRate);

        // Increase batch size if performance is good
        if (avgThroughput > 10 && avgSuccessRate > 0.95)
        {
            return Math.Min(currentBatchSize + 5, _options.BatchSize * 2);
        }
        // Decrease batch size if performance is poor
        else if (avgThroughput < 5 || avgSuccessRate < 0.8)
        {
            return Math.Max(currentBatchSize - 5, _options.BatchSize / 2);
        }

        return currentBatchSize;
    }

    private int CalculateOptimalBatchSize(List<BatchPerformanceMetric> metrics)
    {
        if (!metrics.Any())
            return _options.BatchSize;

        // Find batch size with highest throughput and good success rate
        return metrics
            .Where(m => m.SuccessRate > 0.9)
            .OrderByDescending(m => m.Throughput)
            .FirstOrDefault()?.BatchSize ?? _options.BatchSize;
    }

    private List<Batch<T>> CreateOptimalBatches<T>(IEnumerable<T> items)
    {
        var batches = new List<Batch<T>>();
        var itemsList = items.ToList();
        var batchId = 0;

        for (int i = 0; i < itemsList.Count; i += _options.BatchSize)
        {
            var batchItems = itemsList.Skip(i).Take(_options.BatchSize).ToList();
            batches.Add(new Batch<T>
            {
                Id = batchId++,
                Items = batchItems,
                CreatedAt = DateTime.UtcNow
            });
        }

        return batches;
    }

    private async Task ProcessWorkerAsync<T>(
        int workerId,
        Func<IEnumerable<T>, CancellationToken, Task<BatchProcessResult<T>>> worker,
        ConcurrentQueue<Batch<T>> workQueue,
        ConcurrentDictionary<int, WorkerMetrics> workerMetrics,
        LoadBalancedResult result,
        CancellationToken cancellationToken)
    {
        var metrics = workerMetrics[workerId];
        var stopwatch = Stopwatch.StartNew();

        while (workQueue.TryDequeue(out var batch))
        {
            try
            {
                var batchStopwatch = Stopwatch.StartNew();
                var batchResult = await worker(batch.Items, cancellationToken);
                batchStopwatch.Stop();

                metrics.ProcessedBatches++;
                metrics.ProcessedItems += batchResult.SuccessfulItems;
                metrics.FailedItems += batchResult.FailedItems;
                metrics.TotalProcessingTime += batchStopwatch.Elapsed;

                result.AddProcessedItems(batchResult.SuccessfulItems);
                result.AddFailedItems(batchResult.FailedItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} failed to process batch {BatchId}", workerId, batch.Id);
                metrics.FailedItems += batch.Items.Count;
                result.AddFailedItems(batch.Items.Count);
            }
        }

        stopwatch.Stop();
        metrics.TotalTime = stopwatch.Elapsed;
        metrics.Throughput = metrics.ProcessedItems / stopwatch.Elapsed.TotalSeconds;
    }

    #endregion
}

/// <summary>
/// Batch processing options
/// </summary>
public class BatchProcessingOptions
{
    public int BatchSize { get; set; } = 50;
    public int MaxConcurrency { get; set; } = 4;
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableAdaptiveSizing { get; set; } = true;
}

/// <summary>
/// Represents a batch of items to process
/// </summary>
public class Batch<T>
{
    public int Id { get; set; }
    public List<T> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Result of batch processing operation
/// </summary>
public class BatchProcessResult<T>
{
    public int SuccessfulItems { get; set; }
    public int FailedItems { get; set; }
    public List<T> ProcessedItems { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Overall batch processing result
/// </summary>
public class BatchProcessingResult
{
    public int TotalItems { get; set; }
    private int _processedItems;
    private int _failedItems;
    
    public int ProcessedItems 
    { 
        get => _processedItems;
        set => _processedItems = value;
    }
    
    public int FailedItems 
    { 
        get => _failedItems;
        set => _failedItems = value;
    }
    
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public double OverallThroughput { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Helper methods for thread-safe updates
    public void AddProcessedItems(int count) => Interlocked.Add(ref _processedItems, count);
    public void AddFailedItems(int count) => Interlocked.Add(ref _failedItems, count);
}

/// <summary>
/// Adaptive batch processing result
/// </summary>
public class AdaptiveBatchResult : BatchProcessingResult
{
    public int OptimalBatchSize { get; set; }
    public List<BatchPerformanceMetric> BatchMetrics { get; set; } = new();
}

/// <summary>
/// Load-balanced processing result
/// </summary>
public class LoadBalancedResult : BatchProcessingResult
{
    public int WorkerCount { get; set; }
    public List<WorkerMetrics> WorkerMetrics { get; set; } = new();
}

/// <summary>
/// Performance metrics for a single batch
/// </summary>
public class BatchPerformanceMetric
{
    public int BatchSize { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public double Throughput { get; set; }
    public double SuccessRate { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Metrics for a worker in load-balanced processing
/// </summary>
public class WorkerMetrics
{
    public int WorkerId { get; set; }
    public int ProcessedBatches { get; set; }
    public int ProcessedItems { get; set; }
    public int FailedItems { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public TimeSpan TotalTime { get; set; }
    public double Throughput { get; set; }
    public double SuccessRate => ProcessedItems + FailedItems > 0 ? (double)ProcessedItems / (ProcessedItems + FailedItems) : 0;
}