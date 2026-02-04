using System;

namespace OrderXChange.Integrations.Foodics;

/// <summary>
/// Dead Letter Queue event for failed Menu Sync operations.
/// As per SDD Section 8 - DLQ Strategy.
/// </summary>
[Serializable]
public class MenuSyncFailedEto
{
    public string CorrelationId { get; set; } = null!;
    public Guid AccountId { get; set; }
    public string OriginalMessage { get; set; } = null!; // Serialized MenuSyncEto
    public string ErrorCode { get; set; } = null!;
    public string ErrorMessage { get; set; } = null!;
    public int Attempts { get; set; }
    public DateTime LastAttemptUtc { get; set; }
    public DateTime FirstAttemptUtc { get; set; }
    public string FailureType { get; set; } = null!; // "Transient" or "Permanent"
}

