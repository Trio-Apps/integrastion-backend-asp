using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Staging;

/// <summary>
/// Stores failed messages from Dead Letter Queue for manual replay.
/// Implements SDD Section 8 - DLQ Strategy.
/// </summary>
public class DlqMessage : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	/// <summary>
	/// Type of event that failed: MenuSync, OrderSync, etc.
	/// </summary>
	[Required]
	[MaxLength(100)]
	public string EventType { get; set; } = string.Empty;

	/// <summary>
	/// Correlation ID for tracking across systems
	/// </summary>
	[Required]
	[MaxLength(100)]
	public string CorrelationId { get; set; } = string.Empty;

	/// <summary>
	/// FoodicsAccount ID associated with the message
	/// </summary>
	public Guid? AccountId { get; set; }

	/// <summary>
	/// Original message payload (JSON serialized)
	/// </summary>
	[Required]
	[Column(TypeName = "LONGTEXT")]
	public string OriginalMessage { get; set; } = string.Empty;

	/// <summary>
	/// Error code/exception type
	/// </summary>
	[Required]
	[MaxLength(200)]
	public string ErrorCode { get; set; } = string.Empty;

	/// <summary>
	/// Detailed error message
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Full stack trace (for debugging)
	/// </summary>
	[Column(TypeName = "LONGTEXT")]
	public string? StackTrace { get; set; }

	/// <summary>
	/// Number of processing attempts before DLQ
	/// </summary>
	public int Attempts { get; set; }

	/// <summary>
	/// Type of failure: Transient or Permanent
	/// </summary>
	[Required]
	[MaxLength(50)]
	public string FailureType { get; set; } = "Permanent";

	/// <summary>
	/// When the first processing attempt occurred
	/// </summary>
	public DateTime FirstAttemptUtc { get; set; }

	/// <summary>
	/// When the last processing attempt occurred
	/// </summary>
	public DateTime LastAttemptUtc { get; set; }

	/// <summary>
	/// Whether this message has been replayed
	/// </summary>
	public bool IsReplayed { get; set; }

	/// <summary>
	/// When the message was replayed (if applicable)
	/// </summary>
	public DateTime? ReplayedAt { get; set; }

	/// <summary>
	/// User who initiated the replay (if applicable)
	/// </summary>
	[MaxLength(100)]
	public string? ReplayedBy { get; set; }

	/// <summary>
	/// Result of the replay: Success, Failed
	/// </summary>
	[MaxLength(50)]
	public string? ReplayResult { get; set; }

	/// <summary>
	/// Error message from replay attempt (if failed)
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? ReplayErrorMessage { get; set; }

	/// <summary>
	/// Whether this message has been acknowledged/dismissed
	/// </summary>
	public bool IsAcknowledged { get; set; }

	/// <summary>
	/// When the message was acknowledged
	/// </summary>
	public DateTime? AcknowledgedAt { get; set; }

	/// <summary>
	/// User who acknowledged the message
	/// </summary>
	[MaxLength(100)]
	public string? AcknowledgedBy { get; set; }

	/// <summary>
	/// Notes/comments from operator
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? Notes { get; set; }

	/// <summary>
	/// Priority for processing: Low, Normal, High, Critical
	/// </summary>
	[MaxLength(20)]
	public string Priority { get; set; } = "Normal";

	/// <summary>
	/// Tenant ID for multi-tenancy
	/// </summary>
	public Guid? TenantId { get; set; }
}

/// <summary>
/// Constants for DLQ message types
/// </summary>
public static class DlqEventTypes
{
	public const string MenuSync = "MenuSync";
	public const string OrderSync = "OrderSync";
	public const string AvailabilityUpdate = "AvailabilityUpdate";
	public const string CatalogSync = "CatalogSync";
}

/// <summary>
/// Constants for DLQ failure types
/// </summary>
public static class DlqFailureTypes
{
	public const string Transient = "Transient";
	public const string Permanent = "Permanent";
}

/// <summary>
/// Constants for DLQ priorities
/// </summary>
public static class DlqPriorities
{
	public const string Low = "Low";
	public const string Normal = "Normal";
	public const string High = "High";
	public const string Critical = "Critical";
}

