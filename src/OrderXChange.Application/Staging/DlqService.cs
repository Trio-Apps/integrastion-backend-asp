using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Staging;
using OrderXChange.Domain.Staging;
using OrderXChange.EntityFrameworkCore;
using OrderXChange.Integrations.Foodics;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Staging;

/// <summary>
/// Service for managing Dead Letter Queue messages.
/// Handles storing, replaying, and acknowledging DLQ messages.
/// </summary>
public class DlqService : IDlqService, ITransientDependency
{
	private readonly IRepository<DlqMessage, Guid> _dlqRepository;
	private readonly IDbContextProvider<OrderXChangeDbContext> _dbContextProvider;
	private readonly IDistributedEventBus _eventBus;
	private readonly ILogger<DlqService> _logger;

	public DlqService(
		IRepository<DlqMessage, Guid> dlqRepository,
		IDbContextProvider<OrderXChangeDbContext> dbContextProvider,
		IDistributedEventBus eventBus,
		ILogger<DlqService> logger)
	{
		_dlqRepository = dlqRepository;
		_dbContextProvider = dbContextProvider;
		_eventBus = eventBus;
		_logger = logger;
	}

	/// <summary>
	/// Stores a failed MenuSync event in the DLQ table
	/// </summary>
	[UnitOfWork]
	public virtual async Task<DlqMessage> StoreMenuSyncFailedAsync(
		MenuSyncFailedEto eventData,
		CancellationToken cancellationToken = default)
	{
		var dlqMessage = new DlqMessage
		{
			EventType = DlqEventTypes.MenuSync,
			CorrelationId = eventData.CorrelationId,
			AccountId = eventData.AccountId,
			OriginalMessage = eventData.OriginalMessage,
			ErrorCode = eventData.ErrorCode,
			ErrorMessage = eventData.ErrorMessage,
			Attempts = eventData.Attempts,
			FailureType = eventData.FailureType,
			FirstAttemptUtc = eventData.FirstAttemptUtc,
			LastAttemptUtc = eventData.LastAttemptUtc,
			Priority = DlqPriorities.Normal
		};

		await _dlqRepository.InsertAsync(dlqMessage, autoSave: true, cancellationToken: cancellationToken);

		_logger.LogWarning(
			"Stored DLQ message. Id={DlqId}, EventType={EventType}, CorrelationId={CorrelationId}, AccountId={AccountId}",
			dlqMessage.Id,
			dlqMessage.EventType,
			dlqMessage.CorrelationId,
			dlqMessage.AccountId);

		return dlqMessage;
	}

	/// <summary>
	/// Stores a generic failed event in the DLQ table
	/// </summary>
	[UnitOfWork]
	public virtual async Task<DlqMessage> StoreFailedEventAsync(
		string eventType,
		string correlationId,
		Guid? accountId,
		object originalMessage,
		Exception exception,
		int attempts,
		string failureType,
		DateTime firstAttemptUtc,
		string priority = DlqPriorities.Normal,
		CancellationToken cancellationToken = default)
	{
		var dlqMessage = new DlqMessage
		{
			EventType = eventType,
			CorrelationId = correlationId,
			AccountId = accountId,
			OriginalMessage = JsonSerializer.Serialize(originalMessage),
			ErrorCode = exception.GetType().Name,
			ErrorMessage = exception.Message,
			StackTrace = exception.StackTrace,
			Attempts = attempts,
			FailureType = failureType,
			FirstAttemptUtc = firstAttemptUtc,
			LastAttemptUtc = DateTime.UtcNow,
			Priority = priority
		};

		await _dlqRepository.InsertAsync(dlqMessage, autoSave: true, cancellationToken: cancellationToken);

		_logger.LogWarning(
			"Stored DLQ message. Id={DlqId}, EventType={EventType}, CorrelationId={CorrelationId}",
			dlqMessage.Id,
			dlqMessage.EventType,
			dlqMessage.CorrelationId);

		return dlqMessage;
	}

	/// <summary>
	/// Gets pending DLQ messages (not replayed and not acknowledged) - returns DTOs
	/// </summary>
	async Task<List<DlqMessageDto>> IDlqService.GetPendingMessagesAsync(
		string? eventType,
		string? priority,
		int maxRecords,
		CancellationToken cancellationToken)
	{
		var messages = await GetPendingMessagesInternalAsync(eventType, priority, maxRecords, cancellationToken);
		return messages.Select(MapToDto).ToList();
	}

	/// <summary>
	/// Gets pending DLQ messages (not replayed and not acknowledged) - returns entities
	/// </summary>
	public async Task<List<DlqMessage>> GetPendingMessagesInternalAsync(
		string? eventType = null,
		string? priority = null,
		int maxRecords = 100,
		CancellationToken cancellationToken = default)
	{
		var dbContext = await _dbContextProvider.GetDbContextAsync();

		var query = dbContext.Set<DlqMessage>()
			.Where(x => !x.IsReplayed && !x.IsAcknowledged);

		if (!string.IsNullOrWhiteSpace(eventType))
		{
			query = query.Where(x => x.EventType == eventType);
		}

		if (!string.IsNullOrWhiteSpace(priority))
		{
			query = query.Where(x => x.Priority == priority);
		}

		return await query
			.OrderByDescending(x => x.Priority == DlqPriorities.Critical)
			.ThenByDescending(x => x.Priority == DlqPriorities.High)
			.ThenBy(x => x.LastAttemptUtc)
			.Take(maxRecords)
			.ToListAsync(cancellationToken);
	}

	/// <summary>
	/// Gets a DLQ message by ID - returns DTO
	/// </summary>
	async Task<DlqMessageDto?> IDlqService.GetByIdAsync(Guid id, CancellationToken cancellationToken)
	{
		var message = await GetByIdInternalAsync(id, cancellationToken);
		return message != null ? MapToDto(message) : null;
	}

	/// <summary>
	/// Gets a DLQ message by ID - returns entity
	/// </summary>
	public async Task<DlqMessage?> GetByIdInternalAsync(
		Guid id,
		CancellationToken cancellationToken = default)
	{
		return await _dlqRepository.FindAsync(id, cancellationToken: cancellationToken);
	}

	/// <summary>
	/// Marks a message as replayed
	/// </summary>
	[UnitOfWork]
	public virtual async Task MarkAsReplayedAsync(
		Guid id,
		bool success,
		string? replayedBy = null,
		string? errorMessage = null,
		CancellationToken cancellationToken = default)
	{
		var message = await _dlqRepository.GetAsync(id, cancellationToken: cancellationToken);

		message.IsReplayed = true;
		message.ReplayedAt = DateTime.UtcNow;
		message.ReplayedBy = replayedBy;
		message.ReplayResult = success ? "Success" : "Failed";
		message.ReplayErrorMessage = errorMessage;

		await _dlqRepository.UpdateAsync(message, autoSave: true, cancellationToken: cancellationToken);

		_logger.LogInformation(
			"DLQ message marked as replayed. Id={DlqId}, Success={Success}, ReplayedBy={ReplayedBy}",
			id,
			success,
			replayedBy);
	}

	/// <summary>
	/// Acknowledges a message (manually dismissed)
	/// </summary>
	[UnitOfWork]
	public virtual async Task AcknowledgeAsync(
		Guid id,
		string? acknowledgedBy = null,
		string? notes = null,
		CancellationToken cancellationToken = default)
	{
		var message = await _dlqRepository.GetAsync(id, cancellationToken: cancellationToken);

		message.IsAcknowledged = true;
		message.AcknowledgedAt = DateTime.UtcNow;
		message.AcknowledgedBy = acknowledgedBy;
		message.Notes = notes;

		await _dlqRepository.UpdateAsync(message, autoSave: true, cancellationToken: cancellationToken);

		_logger.LogInformation(
			"DLQ message acknowledged. Id={DlqId}, AcknowledgedBy={AcknowledgedBy}",
			id,
			acknowledgedBy);
	}

	/// <summary>
	/// Updates priority of a message
	/// </summary>
	[UnitOfWork]
	public virtual async Task UpdatePriorityAsync(
		Guid id,
		string priority,
		CancellationToken cancellationToken = default)
	{
		var message = await _dlqRepository.GetAsync(id, cancellationToken: cancellationToken);

		message.Priority = priority;

		await _dlqRepository.UpdateAsync(message, autoSave: true, cancellationToken: cancellationToken);

		_logger.LogInformation(
			"DLQ message priority updated. Id={DlqId}, Priority={Priority}",
			id,
			priority);
	}

	/// <summary>
	/// Gets DLQ statistics - returns DTO
	/// </summary>
	async Task<DlqStatisticsDto> IDlqService.GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var stats = await GetStatisticsInternalAsync(cancellationToken);
		return new DlqStatisticsDto
		{
			TotalMessages = stats.TotalMessages,
			PendingMessages = stats.PendingMessages,
			ReplayedMessages = stats.ReplayedMessages,
			AcknowledgedMessages = stats.AcknowledgedMessages,
			ByEventType = stats.ByEventType.ToDictionary(
				kvp => kvp.Key,
				kvp => new DlqEventTypeStatsDto { Total = kvp.Value.Total, Pending = kvp.Value.Pending })
		};
	}

	/// <summary>
	/// Gets DLQ statistics - internal
	/// </summary>
	public async Task<DlqStatistics> GetStatisticsInternalAsync(CancellationToken cancellationToken = default)
	{
		var dbContext = await _dbContextProvider.GetDbContextAsync();

		var messages = await dbContext.Set<DlqMessage>()
			.GroupBy(x => new { x.EventType, x.IsReplayed, x.IsAcknowledged })
			.Select(g => new
			{
				g.Key.EventType,
				g.Key.IsReplayed,
				g.Key.IsAcknowledged,
				Count = g.Count()
			})
			.ToListAsync(cancellationToken);

		return new DlqStatistics
		{
			TotalMessages = messages.Sum(x => x.Count),
			PendingMessages = messages.Where(x => !x.IsReplayed && !x.IsAcknowledged).Sum(x => x.Count),
			ReplayedMessages = messages.Where(x => x.IsReplayed).Sum(x => x.Count),
			AcknowledgedMessages = messages.Where(x => x.IsAcknowledged).Sum(x => x.Count),
			ByEventType = messages
				.GroupBy(x => x.EventType)
				.ToDictionary(
					g => g.Key,
					g => new DlqEventTypeStats
					{
						Total = g.Sum(x => x.Count),
						Pending = g.Where(x => !x.IsReplayed && !x.IsAcknowledged).Sum(x => x.Count)
					})
		};
	}

	/// <summary>
	/// Replays a MenuSync message from DLQ
	/// </summary>
	[UnitOfWork]
	public virtual async Task ReplayMenuSyncAsync(Guid dlqMessageId, CancellationToken cancellationToken = default)
	{
		var message = await _dlqRepository.GetAsync(dlqMessageId, cancellationToken: cancellationToken);

		if (message.EventType != DlqEventTypes.MenuSync)
		{
			throw new InvalidOperationException($"Cannot replay non-MenuSync message. EventType: {message.EventType}");
		}

		// Deserialize the original MenuSync event
		var originalEvent = JsonSerializer.Deserialize<MenuSyncEto>(message.OriginalMessage);

		if (originalEvent == null)
		{
			throw new InvalidOperationException("Failed to deserialize original MenuSyncEto from DLQ message");
		}

		// Generate new idempotency key for the replay (allows reprocessing)
		var newCorrelationId = Guid.NewGuid().ToString();

		var replayEvent = new MenuSyncEto
		{
			Schema = originalEvent.Schema,
			CorrelationId = newCorrelationId,
			AccountId = originalEvent.AccountId,
			FoodicsAccountId = originalEvent.FoodicsAccountId,
			BranchId = originalEvent.BranchId,
			TenantId = originalEvent.TenantId,
			IdempotencyKey = $"replay:{dlqMessageId}:{DateTime.UtcNow:yyyyMMddHHmmss}",
			OccurredAt = DateTime.UtcNow
		};

		_logger.LogInformation(
			"Replaying MenuSync event from DLQ. DlqId={DlqId}, OriginalCorrelationId={OriginalCorrelationId}, NewCorrelationId={NewCorrelationId}",
			dlqMessageId,
			originalEvent.CorrelationId,
			newCorrelationId);

		await _eventBus.PublishAsync(replayEvent);
	}

	#region Private Methods

	private static DlqMessageDto MapToDto(DlqMessage entity)
	{
		return new DlqMessageDto
		{
			Id = entity.Id,
			EventType = entity.EventType,
			CorrelationId = entity.CorrelationId,
			AccountId = entity.AccountId,
			OriginalMessage = entity.OriginalMessage,
			ErrorCode = entity.ErrorCode,
			ErrorMessage = entity.ErrorMessage,
			StackTrace = entity.StackTrace,
			Attempts = entity.Attempts,
			FailureType = entity.FailureType,
			Priority = entity.Priority,
			FirstAttemptUtc = entity.FirstAttemptUtc,
			LastAttemptUtc = entity.LastAttemptUtc,
			IsReplayed = entity.IsReplayed,
			ReplayedAt = entity.ReplayedAt,
			ReplayedBy = entity.ReplayedBy,
			ReplayResult = entity.ReplayResult,
			ReplayErrorMessage = entity.ReplayErrorMessage,
			IsAcknowledged = entity.IsAcknowledged,
			AcknowledgedAt = entity.AcknowledgedAt,
			AcknowledgedBy = entity.AcknowledgedBy,
			Notes = entity.Notes,
			CreationTime = entity.CreationTime
		};
	}

	#endregion
}

/// <summary>
/// DLQ statistics (internal)
/// </summary>
public class DlqStatistics
{
	public int TotalMessages { get; set; }
	public int PendingMessages { get; set; }
	public int ReplayedMessages { get; set; }
	public int AcknowledgedMessages { get; set; }
	public Dictionary<string, DlqEventTypeStats> ByEventType { get; set; } = new();
}

/// <summary>
/// Statistics per event type (internal)
/// </summary>
public class DlqEventTypeStats
{
	public int Total { get; set; }
	public int Pending { get; set; }
}
