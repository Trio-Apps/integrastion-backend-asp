using System;

namespace OrderXChange.Integrations.Talabat;

/// <summary>
/// Retry event for Talabat order dispatch operations.
/// Represents a logical Kafka retry topic (e.g. order.dispatch.retry.1m/5m/15m).
/// </summary>
[Serializable]
public class OrderDispatchRetryEto
{
    public OrderDispatchEto Message { get; set; } = new();
    public int Attempts { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime LastAttemptUtc { get; set; } = DateTime.UtcNow;
    public int RetryDelaySeconds { get; set; }
    public string FailureType { get; set; } = "Transient";
}
