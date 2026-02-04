using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OrderXChange.Application.Versioning.DTOs;

/// <summary>
/// Request for storing a failed menu sync operation in DLQ
/// </summary>
public class MenuSyncDlqRequest
{
    /// <summary>
    /// Type of menu sync operation that failed
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Foodics Account ID
    /// </summary>
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID (optional)
    /// </summary>
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group ID (optional)
    /// </summary>
    public Guid? MenuGroupId { get; set; }

    /// <summary>
    /// Delta ID (for delta sync failures)
    /// </summary>
    public Guid? DeltaId { get; set; }

    /// <summary>
    /// Talabat vendor code (for sync failures)
    /// </summary>
    public string? TalabatVendorCode { get; set; }

    /// <summary>
    /// Original operation payload (JSON serialized)
    /// </summary>
    public string OriginalPayload { get; set; } = string.Empty;

    /// <summary>
    /// Exception that caused the failure
    /// </summary>
    public Exception Exception { get; set; } = null!;

    /// <summary>
    /// Number of attempts made before DLQ
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Failure type: Transient or Permanent
    /// </summary>
    public string FailureType { get; set; } = "Permanent";

    /// <summary>
    /// Priority for processing
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Additional context information
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// When the first attempt occurred
    /// </summary>
    public DateTime FirstAttemptUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of replaying a DLQ message
/// </summary>
public class MenuSyncDlqReplayResult
{
    /// <summary>
    /// Whether the replay was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// DLQ message ID that was replayed
    /// </summary>
    public Guid DlqMessageId { get; set; }

    /// <summary>
    /// Error message if replay failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken for the replay
    /// </summary>
    public TimeSpan ReplayTime { get; set; }

    /// <summary>
    /// Result of the replayed operation (if successful)
    /// </summary>
    public object? OperationResult { get; set; }

    /// <summary>
    /// When the replay was executed
    /// </summary>
    public DateTime ReplayedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of bulk replaying DLQ messages
/// </summary>
public class MenuSyncDlqBulkReplayResult
{
    /// <summary>
    /// Total number of messages attempted
    /// </summary>
    public int TotalAttempted { get; set; }

    /// <summary>
    /// Number of successful replays
    /// </summary>
    public int SuccessfulReplays { get; set; }

    /// <summary>
    /// Number of failed replays
    /// </summary>
    public int FailedReplays { get; set; }

    /// <summary>
    /// Individual replay results
    /// </summary>
    public List<MenuSyncDlqReplayResult> Results { get; set; } = new();

    /// <summary>
    /// Total time taken for bulk replay
    /// </summary>
    public TimeSpan TotalTime { get; set; }

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalAttempted > 0 ? (double)SuccessfulReplays / TotalAttempted * 100 : 0;
}

/// <summary>
/// DLQ statistics for monitoring
/// </summary>
public class MenuSyncDlqStatistics
{
    /// <summary>
    /// Foodics Account ID
    /// </summary>
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Statistics period start
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// Statistics period end
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Total messages in DLQ
    /// </summary>
    public int TotalMessages { get; set; }

    /// <summary>
    /// Pending messages (not replayed or acknowledged)
    /// </summary>
    public int PendingMessages { get; set; }

    /// <summary>
    /// Successfully replayed messages
    /// </summary>
    public int ReplayedMessages { get; set; }

    /// <summary>
    /// Acknowledged messages
    /// </summary>
    public int AcknowledgedMessages { get; set; }

    /// <summary>
    /// Messages by event type
    /// </summary>
    public Dictionary<string, int> MessagesByEventType { get; set; } = new();

    /// <summary>
    /// Messages by failure type
    /// </summary>
    public Dictionary<string, int> MessagesByFailureType { get; set; } = new();

    /// <summary>
    /// Messages by priority
    /// </summary>
    public Dictionary<string, int> MessagesByPriority { get; set; } = new();

    /// <summary>
    /// Average time to resolution
    /// </summary>
    public double AverageResolutionTimeHours { get; set; }

    /// <summary>
    /// Replay success rate
    /// </summary>
    public double ReplaySuccessRate { get; set; }
}

/// <summary>
/// Result of DLQ cleanup operation
/// </summary>
public class MenuSyncDlqCleanupResult
{
    /// <summary>
    /// Number of messages deleted
    /// </summary>
    public int DeletedMessages { get; set; }

    /// <summary>
    /// Storage space freed (bytes)
    /// </summary>
    public long FreedStorageBytes { get; set; }

    /// <summary>
    /// Time taken for cleanup
    /// </summary>
    public TimeSpan CleanupTime { get; set; }

    /// <summary>
    /// Any errors encountered during cleanup
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of auto-retry operation for transient failures
/// </summary>
public class MenuSyncDlqAutoRetryResult
{
    /// <summary>
    /// Number of messages eligible for retry
    /// </summary>
    public int EligibleMessages { get; set; }

    /// <summary>
    /// Number of successful retries
    /// </summary>
    public int SuccessfulRetries { get; set; }

    /// <summary>
    /// Number of failed retries
    /// </summary>
    public int FailedRetries { get; set; }

    /// <summary>
    /// Messages that exceeded retry limits
    /// </summary>
    public int ExceededRetryLimit { get; set; }

    /// <summary>
    /// Time taken for auto-retry operation
    /// </summary>
    public TimeSpan RetryTime { get; set; }

    /// <summary>
    /// Individual retry results
    /// </summary>
    public List<MenuSyncDlqReplayResult> RetryResults { get; set; } = new();

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => EligibleMessages > 0 ? (double)SuccessfulRetries / EligibleMessages * 100 : 0;
}

/// <summary>
/// Constants for Menu Sync DLQ event types
/// </summary>
public static class MenuSyncDlqEventTypes
{
    public const string DeltaGeneration = "MenuSync.DeltaGeneration";
    public const string DeltaSync = "MenuSync.DeltaSync";
    public const string DeltaValidation = "MenuSync.DeltaValidation";
    public const string ModifierSync = "MenuSync.ModifierSync";
    public const string SoftDeleteProcessing = "MenuSync.SoftDeleteProcessing";
    public const string MappingResolution = "MenuSync.MappingResolution";
    public const string TalabatApiCall = "MenuSync.TalabatApiCall";
}

/// <summary>
/// Menu Sync specific failure types
/// </summary>
public static class MenuSyncFailureTypes
{
    public const string ValidationFailure = "ValidationFailure";
    public const string ApiTimeout = "ApiTimeout";
    public const string ApiRateLimit = "ApiRateLimit";
    public const string NetworkError = "NetworkError";
    public const string DataCorruption = "DataCorruption";
    public const string MappingError = "MappingError";
    public const string AuthenticationError = "AuthenticationError";
    public const string BusinessRuleViolation = "BusinessRuleViolation";
}