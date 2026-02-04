using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Staging;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service interface for Menu Sync Dead Letter Queue operations
/// Handles failed payload storage, manual replay, and DLQ management
/// </summary>
public interface IMenuSyncDlqService
{
    /// <summary>
    /// Stores a failed menu sync operation in the DLQ
    /// </summary>
    /// <param name="request">DLQ storage request with failure details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created DLQ message ID</returns>
    Task<Guid> StoreDeltaSyncFailureAsync(
        MenuSyncDlqRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a failed delta generation operation in the DLQ
    /// </summary>
    /// <param name="request">DLQ storage request with failure details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created DLQ message ID</returns>
    Task<Guid> StoreDeltaGenerationFailureAsync(
        MenuSyncDlqRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a failed validation operation in the DLQ
    /// </summary>
    /// <param name="request">DLQ storage request with failure details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created DLQ message ID</returns>
    Task<Guid> StoreValidationFailureAsync(
        MenuSyncDlqRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays a DLQ message by re-executing the failed operation
    /// </summary>
    /// <param name="dlqMessageId">DLQ message ID to replay</param>
    /// <param name="replayedBy">User initiating the replay</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Replay result</returns>
    Task<MenuSyncDlqReplayResult> ReplayMessageAsync(
        Guid dlqMessageId,
        string replayedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk replays multiple DLQ messages
    /// </summary>
    /// <param name="dlqMessageIds">DLQ message IDs to replay</param>
    /// <param name="replayedBy">User initiating the replay</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bulk replay results</returns>
    Task<MenuSyncDlqBulkReplayResult> ReplayMessagesAsync(
        List<Guid> dlqMessageIds,
        string replayedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending DLQ messages for a specific account
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="eventType">Event type filter (optional)</param>
    /// <param name="priority">Priority filter (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending DLQ messages</returns>
    Task<List<DlqMessage>> GetPendingMessagesAsync(
        Guid foodicsAccountId,
        string? eventType = null,
        string? priority = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets DLQ statistics for monitoring and analytics
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="fromDate">Start date for statistics</param>
    /// <param name="toDate">End date for statistics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DLQ statistics</returns>
    Task<MenuSyncDlqStatistics> GetDlqStatisticsAsync(
        Guid foodicsAccountId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges a DLQ message (marks as resolved)
    /// </summary>
    /// <param name="dlqMessageId">DLQ message ID</param>
    /// <param name="acknowledgedBy">User acknowledging the message</param>
    /// <param name="notes">Optional notes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    Task AcknowledgeMessageAsync(
        Guid dlqMessageId,
        string acknowledgedBy,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the priority of a DLQ message
    /// </summary>
    /// <param name="dlqMessageId">DLQ message ID</param>
    /// <param name="priority">New priority</param>
    /// <param name="updatedBy">User updating the priority</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    Task UpdateMessagePriorityAsync(
        Guid dlqMessageId,
        string priority,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs DLQ cleanup - removes old acknowledged messages
    /// </summary>
    /// <param name="retentionDays">Number of days to retain messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cleanup statistics</returns>
    Task<MenuSyncDlqCleanupResult> CleanupOldMessagesAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-retries transient failures that haven't exceeded retry limits
    /// </summary>
    /// <param name="maxAge">Maximum age of messages to retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auto-retry results</returns>
    Task<MenuSyncDlqAutoRetryResult> AutoRetryTransientFailuresAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default);
}