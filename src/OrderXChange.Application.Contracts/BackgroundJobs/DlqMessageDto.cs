using System;

namespace OrderXChange.BackgroundJobs;

/// <summary>
/// DTO for DLQ message information
/// </summary>
public class DlqMessageDto
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

