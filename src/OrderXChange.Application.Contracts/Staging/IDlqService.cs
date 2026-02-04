using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrderXChange.Application.Contracts.Staging;

/// <summary>
/// Interface for managing Dead Letter Queue messages.
/// Handles storing, replaying, and acknowledging DLQ messages.
/// </summary>
public interface IDlqService
{
	/// <summary>
	/// Gets pending DLQ messages (not replayed and not acknowledged)
	/// </summary>
	Task<List<DlqMessageDto>> GetPendingMessagesAsync(
		string? eventType = null,
		string? priority = null,
		int maxRecords = 100,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a DLQ message by ID
	/// </summary>
	Task<DlqMessageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Marks a message as replayed
	/// </summary>
	Task MarkAsReplayedAsync(
		Guid id,
		bool success,
		string? replayedBy = null,
		string? errorMessage = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Acknowledges a message (manually dismissed)
	/// </summary>
	Task AcknowledgeAsync(
		Guid id,
		string? acknowledgedBy = null,
		string? notes = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates priority of a message
	/// </summary>
	Task UpdatePriorityAsync(
		Guid id,
		string priority,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets DLQ statistics
	/// </summary>
	Task<DlqStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Replays a MenuSync message from DLQ
	/// </summary>
	Task ReplayMenuSyncAsync(Guid dlqMessageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for DLQ message
/// </summary>
public class DlqMessageDto
{
	public Guid Id { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string CorrelationId { get; set; } = string.Empty;
	public Guid? AccountId { get; set; }
	public string OriginalMessage { get; set; } = string.Empty;
	public string ErrorCode { get; set; } = string.Empty;
	public string? ErrorMessage { get; set; }
	public string? StackTrace { get; set; }
	public int Attempts { get; set; }
	public string FailureType { get; set; } = string.Empty;
	public string Priority { get; set; } = string.Empty;
	public DateTime FirstAttemptUtc { get; set; }
	public DateTime LastAttemptUtc { get; set; }
	public bool IsReplayed { get; set; }
	public DateTime? ReplayedAt { get; set; }
	public string? ReplayedBy { get; set; }
	public string? ReplayResult { get; set; }
	public string? ReplayErrorMessage { get; set; }
	public bool IsAcknowledged { get; set; }
	public DateTime? AcknowledgedAt { get; set; }
	public string? AcknowledgedBy { get; set; }
	public string? Notes { get; set; }
	public DateTime CreationTime { get; set; }
}

/// <summary>
/// DLQ statistics
/// </summary>
public class DlqStatisticsDto
{
	public int TotalMessages { get; set; }
	public int PendingMessages { get; set; }
	public int ReplayedMessages { get; set; }
	public int AcknowledgedMessages { get; set; }
	public Dictionary<string, DlqEventTypeStatsDto> ByEventType { get; set; } = new();
}

/// <summary>
/// Statistics per event type
/// </summary>
public class DlqEventTypeStatsDto
{
	public int Total { get; set; }
	public int Pending { get; set; }
}

/// <summary>
/// Constants for DLQ event types
/// </summary>
public static class DlqEventTypesConsts
{
	public const string MenuSync = "MenuSync";
	public const string OrderSync = "OrderSync";
	public const string AvailabilityUpdate = "AvailabilityUpdate";
	public const string CatalogSync = "CatalogSync";
}

/// <summary>
/// Constants for DLQ priorities
/// </summary>
public static class DlqPrioritiesConsts
{
	public const string Low = "Low";
	public const string Normal = "Normal";
	public const string High = "High";
	public const string Critical = "Critical";
}

