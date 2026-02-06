using System;

namespace OrderXChange.Integrations.Talabat;

/// <summary>
/// Dead Letter Queue event for failed order dispatch operations.
/// </summary>
[Serializable]
public class OrderDispatchFailedEto
{
    public string CorrelationId { get; set; } = null!;
    public Guid AccountId { get; set; }
    public string OriginalMessage { get; set; } = null!;
    public string ErrorCode { get; set; } = null!;
    public string ErrorMessage { get; set; } = null!;
    public int Attempts { get; set; }
    public DateTime LastAttemptUtc { get; set; }
    public DateTime FirstAttemptUtc { get; set; }
    public string FailureType { get; set; } = null!;
}
