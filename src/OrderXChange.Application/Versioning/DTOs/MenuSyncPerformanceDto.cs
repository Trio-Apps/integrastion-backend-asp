using OrderXChange.Application.Integrations.Foodics;
using System;
using System.Collections.Generic;

namespace OrderXChange.Application.Versioning.DTOs;

/// <summary>
/// Configuration options for menu sync performance optimization
/// </summary>
public class MenuSyncPerformanceOptions
{
    public const string SectionName = "MenuSync:Performance";

    /// <summary>
    /// Maximum number of concurrent sync operations
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Number of payloads per batch
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Maximum batch size in bytes
    /// </summary>
    public int MaxBatchSizeBytes { get; set; } = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// Maximum number of products per API request batch
    /// </summary>
    public int MaxApiRequestBatchSize { get; set; } = 100;

    /// <summary>
    /// Default cache TTL for frequently accessed data
    /// </summary>
    public TimeSpan DefaultCacheTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Enable payload compression
    /// </summary>
    public bool EnablePayloadCompression { get; set; } = true;

    /// <summary>
    /// Enable smart caching
    /// </summary>
    public bool EnableSmartCaching { get; set; } = true;

    /// <summary>
    /// Enable parallel processing
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;

    /// <summary>
    /// Connection pool size for HTTP clients
    /// </summary>
    public int HttpConnectionPoolSize { get; set; } = 20;

    /// <summary>
    /// Request timeout for API calls
    /// </summary>
    public TimeSpan ApiRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Optimized menu delta payload with compression and reference optimization
/// </summary>
public class OptimizedMenuDeltaPayload
{
    /// <summary>
    /// Optimized metadata with minimal fields
    /// </summary>
    public OptimizedDeltaMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Compressed payload data
    /// </summary>
    public byte[] CompressedData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Reference map for deduplication
    /// </summary>
    public Dictionary<string, object> ReferenceMap { get; set; } = new();

    /// <summary>
    /// Optimization statistics
    /// </summary>
    public PayloadOptimizationStats OptimizationStats { get; set; } = new();
}

/// <summary>
/// Optimized delta metadata with minimal footprint
/// </summary>
public class OptimizedDeltaMetadata
{
    public Guid DeltaId { get; set; }
    public Guid AccountId { get; set; }
    public int Version { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int ChangeCount { get; set; }
    public CompactDeltaStats CompactStats { get; set; } = new();
}

/// <summary>
/// Compact delta statistics
/// </summary>
public class CompactDeltaStats
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Removed { get; set; }
}

/// <summary>
/// Payload optimization statistics
/// </summary>
public class PayloadOptimizationStats
{
    public int OriginalSizeBytes { get; set; }
    public int OptimizedSizeBytes { get; set; }
    public double CompressionRatio { get; set; }
    public TimeSpan OptimizationTime { get; set; }
    public int DeduplicatedReferences { get; set; }
    public int CompressedFields { get; set; }
}

/// <summary>
/// Result of parallel sync execution
/// </summary>
public class ParallelSyncResult
{
    public int TotalPayloads { get; set; }
    public int SuccessfulSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public double OverallThroughput { get; set; }
    public string? ErrorMessage { get; set; }
    public List<BatchSyncResult> BatchResults { get; set; } = new();

    public double SuccessRate => TotalPayloads > 0 ? (double)SuccessfulSyncs / TotalPayloads * 100 : 0;
    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
}

/// <summary>
/// Result of batch sync execution
/// </summary>
public class BatchSyncResult
{
    public int BatchIndex { get; set; }
    public int PayloadCount { get; set; }
    public int SuccessfulSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public double Throughput { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Batched API request for optimized processing
/// </summary>
public class BatchedApiRequest
{
    public int BatchId { get; set; }
    public List<FoodicsProductDetailDto> Products { get; set; } = new();
    public BatchPriority Priority { get; set; } = BatchPriority.Normal;
    public string? CategoryId { get; set; }
    public TimeSpan EstimatedProcessingTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Batch priority levels
/// </summary>
public enum BatchPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Batching strategy options
/// </summary>
public enum BatchingStrategy
{
    /// <summary>
    /// Maximize throughput with larger batches
    /// </summary>
    MaxThroughput,

    /// <summary>
    /// Minimize latency with smaller batches
    /// </summary>
    MinLatency,

    /// <summary>
    /// Balance between throughput and latency
    /// </summary>
    Balanced,

    /// <summary>
    /// Group by category for cache locality
    /// </summary>
    CategoryGrouped
}

/// <summary>
/// Performance metrics for monitoring
/// </summary>
public class MenuSyncPerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid FoodicsAccountId { get; set; }
    public string Operation { get; set; } = string.Empty;
    
    // Throughput metrics
    public double ItemsPerSecond { get; set; }
    public double RequestsPerSecond { get; set; }
    public double BytesPerSecond { get; set; }
    
    // Latency metrics
    public TimeSpan AverageLatency { get; set; }
    public TimeSpan P95Latency { get; set; }
    public TimeSpan P99Latency { get; set; }
    
    // Resource utilization
    public double CpuUtilization { get; set; }
    public long MemoryUsageBytes { get; set; }
    public int ActiveConnections { get; set; }
    
    // Error rates
    public double ErrorRate { get; set; }
    public double TimeoutRate { get; set; }
    public double RetryRate { get; set; }
    
    // Optimization effectiveness
    public double CompressionRatio { get; set; }
    public double CacheHitRate { get; set; }
    public int BatchEfficiency { get; set; }
}

/// <summary>
/// Performance optimization recommendations
/// </summary>
public class MenuSyncPerformanceRecommendations
{
    public Guid FoodicsAccountId { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    public int RecommendedConcurrency { get; set; }
    public int RecommendedBatchSize { get; set; }
    public BatchingStrategy RecommendedStrategy { get; set; }
    public TimeSpan RecommendedCacheTtl { get; set; }
    
    public List<string> Recommendations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    
    public Dictionary<string, object> OptimizationParameters { get; set; } = new();
}

/// <summary>
/// Smart caching configuration
/// </summary>
public class SmartCacheConfiguration
{
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan CategoryDataTtl { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ModifierDataTtl { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan MappingDataTtl { get; set; } = TimeSpan.FromHours(2);
    
    public int MaxCacheSize { get; set; } = 10000;
    public bool EnableDistributedCache { get; set; } = true;
    public bool EnableCompressionInCache { get; set; } = true;
    
    public List<string> CacheKeys { get; set; } = new()
    {
        "categories:{accountId}",
        "modifiers:{accountId}",
        "mappings:{accountId}:{branchId}",
        "snapshots:{accountId}:{version}"
    };
}