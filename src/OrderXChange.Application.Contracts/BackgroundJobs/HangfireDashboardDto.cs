using System;
using System.Collections.Generic;

namespace OrderXChange.BackgroundJobs;

public class HangfireDashboardDto
{
    public HangfireJobCountDto Counts { get; set; } = new();
    public List<HangfireJobItemDto> SucceededJobs { get; set; } = new();
    public List<HangfireJobItemDto> FailedJobs { get; set; } = new();
    public List<HangfireJobItemDto> ProcessingJobs { get; set; } = new();
    public List<HangfireJobItemDto> ScheduledJobs { get; set; } = new();
    public List<HangfireJobItemDto> EnqueuedJobs { get; set; } = new();
}

public class HangfireJobCountDto
{
    public long Succeeded { get; set; }
    public long Failed { get; set; }
    public long Processing { get; set; }
    public long Scheduled { get; set; }
    public long Enqueued { get; set; }
    public long AwaitingRetry { get; set; }
    public long Deleted { get; set; }
}

public class HangfireJobItemDto
{
    public string Id { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? JobName { get; set; }
    public string? Queue { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? EnqueuedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Arguments { get; set; }
    public string? ExceptionMessage { get; set; }
}

