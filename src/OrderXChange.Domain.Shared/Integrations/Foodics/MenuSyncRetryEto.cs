using System;

namespace OrderXChange.Integrations.Foodics;

/// <summary>
/// Retry event for Menu Sync operations.
/// Represents a logical Kafka retry topic (e.g. menu.sync.retry.1m/5m/15m).
/// </summary>
[Serializable]
public class MenuSyncRetryEto
{
    /// <summary>
    /// The original menu sync message to retry.
    /// </summary>
    public MenuSyncEto Message { get; set; } = new();

    /// <summary>
    /// Number of attempts that have already been made for this message.
    /// (First failure from main topic => Attempts = 1)
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Last error code (exception type or HTTP status family).
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Last error message for observability and debugging.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// When the last attempt happened.
    /// </summary>
    public DateTime LastAttemptUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Delay (seconds) before this retry should be executed.
    /// For documentation/monitoring; the actual delay is enforced by Hangfire scheduling.
    /// Typical values: 60s, 300s, 900s.
    /// </summary>
    public int RetryDelaySeconds { get; set; }

    /// <summary>
    /// Failure type classification as per SDD §8.1 ("Transient" or "Permanent").
    /// </summary>
    public string FailureType { get; set; } = "Transient";
}


