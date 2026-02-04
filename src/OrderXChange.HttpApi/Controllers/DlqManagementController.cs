using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Staging;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

/// <summary>
/// API controller for managing Dead Letter Queue (DLQ) messages
/// Implements SDD Section 8.3 - Remediation & Replay
/// </summary>
[Route("api/dlq")]
[AllowAnonymous] // TODO: Change back to [Authorize] in production
public class DlqManagementController : AbpController
{
	private readonly IDlqService _dlqService;
	private readonly ILogger<DlqManagementController> _logger;

	public DlqManagementController(
		IDlqService dlqService,
		ILogger<DlqManagementController> logger)
	{
		_dlqService = dlqService;
		_logger = logger;
	}

	/// <summary>
	/// Get DLQ statistics overview
	/// </summary>
	[HttpGet("stats")]
	public async Task<IActionResult> GetDlqStatsAsync(CancellationToken cancellationToken = default)
	{
		var stats = await _dlqService.GetStatisticsAsync(cancellationToken);

		return Ok(new
		{
			success = true,
			stats.TotalMessages,
			stats.PendingMessages,
			stats.ReplayedMessages,
			stats.AcknowledgedMessages,
			ByEventType = stats.ByEventType,
			timestamp = DateTime.UtcNow
		});
	}

	/// <summary>
	/// Get pending DLQ messages (not replayed and not acknowledged)
	/// </summary>
	[HttpGet("messages")]
	public async Task<IActionResult> GetPendingMessagesAsync(
		[FromQuery] string? eventType = null,
		[FromQuery] string? priority = null,
		[FromQuery] int maxRecords = 100,
		CancellationToken cancellationToken = default)
	{
		var messages = await _dlqService.GetPendingMessagesAsync(eventType, priority, maxRecords, cancellationToken);

		return Ok(new
		{
			success = true,
			count = messages.Count,
			messages = messages.Select(m => new
			{
				m.Id,
				m.EventType,
				m.CorrelationId,
				m.AccountId,
				m.ErrorCode,
				m.ErrorMessage,
				m.Attempts,
				m.FailureType,
				m.Priority,
				m.FirstAttemptUtc,
				m.LastAttemptUtc,
				m.IsReplayed,
				m.IsAcknowledged
			}),
			timestamp = DateTime.UtcNow
		});
	}

	/// <summary>
	/// Get a specific DLQ message by ID (includes full original message)
	/// </summary>
	[HttpGet("messages/{id:guid}")]
	public async Task<IActionResult> GetMessageByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default)
	{
		var message = await _dlqService.GetByIdAsync(id, cancellationToken);

		if (message == null)
		{
			return NotFound(new { success = false, message = $"DLQ message with ID {id} not found" });
		}

		return Ok(new
		{
			success = true,
			message = new
			{
				message.Id,
				message.EventType,
				message.CorrelationId,
				message.AccountId,
				message.OriginalMessage,
				message.ErrorCode,
				message.ErrorMessage,
				message.StackTrace,
				message.Attempts,
				message.FailureType,
				message.Priority,
				message.FirstAttemptUtc,
				message.LastAttemptUtc,
				message.IsReplayed,
				message.ReplayedAt,
				message.ReplayedBy,
				message.ReplayResult,
				message.ReplayErrorMessage,
				message.IsAcknowledged,
				message.AcknowledgedAt,
				message.AcknowledgedBy,
				message.Notes,
				message.CreationTime
			}
		});
	}

	/// <summary>
	/// Replay a DLQ message (re-publish to main topic)
	/// </summary>
	[HttpPost("messages/{id:guid}/replay")]
	public async Task<IActionResult> ReplayMessageAsync(
		Guid id,
		CancellationToken cancellationToken = default)
	{
		var message = await _dlqService.GetByIdAsync(id, cancellationToken);

		if (message == null)
		{
			return NotFound(new { success = false, message = $"DLQ message with ID {id} not found" });
		}

		if (message.IsReplayed)
		{
			return BadRequest(new
			{
				success = false,
				message = "Message has already been replayed",
				replayedAt = message.ReplayedAt,
				replayResult = message.ReplayResult
			});
		}

		try
		{
			// Replay based on event type
			switch (message.EventType)
			{
				case DlqEventTypesConsts.MenuSync:
					await _dlqService.ReplayMenuSyncAsync(id, cancellationToken);
					break;

				default:
					return BadRequest(new
					{
						success = false,
						message = $"Replay not implemented for event type: {message.EventType}"
					});
			}

			// Mark as replayed successfully
			await _dlqService.MarkAsReplayedAsync(
				id,
				success: true,
				replayedBy: CurrentUser.UserName ?? "system",
				cancellationToken: cancellationToken);

			_logger.LogInformation(
				"DLQ message replayed successfully. Id={DlqId}, EventType={EventType}, ReplayedBy={ReplayedBy}",
				id,
				message.EventType,
				CurrentUser.UserName);

			return Ok(new
			{
				success = true,
				message = "Message replayed successfully",
				dlqId = id,
				eventType = message.EventType,
				replayedAt = DateTime.UtcNow
			});
		}
		catch (Exception ex)
		{
			// Mark as replay failed
			await _dlqService.MarkAsReplayedAsync(
				id,
				success: false,
				replayedBy: CurrentUser.UserName ?? "system",
				errorMessage: ex.Message,
				cancellationToken: cancellationToken);

			_logger.LogError(
				ex,
				"Failed to replay DLQ message. Id={DlqId}, Error={Error}",
				id,
				ex.Message);

			return StatusCode(500, new
			{
				success = false,
				message = "Failed to replay message",
				error = ex.Message,
				dlqId = id
			});
		}
	}

	/// <summary>
	/// Acknowledge/dismiss a DLQ message without replay
	/// </summary>
	[HttpPost("messages/{id:guid}/acknowledge")]
	public async Task<IActionResult> AcknowledgeMessageAsync(
		Guid id,
		[FromBody] AcknowledgeRequest? request = null,
		CancellationToken cancellationToken = default)
	{
		var message = await _dlqService.GetByIdAsync(id, cancellationToken);

		if (message == null)
		{
			return NotFound(new { success = false, message = $"DLQ message with ID {id} not found" });
		}

		if (message.IsAcknowledged)
		{
			return BadRequest(new
			{
				success = false,
				message = "Message has already been acknowledged",
				acknowledgedAt = message.AcknowledgedAt
			});
		}

		await _dlqService.AcknowledgeAsync(
			id,
			acknowledgedBy: CurrentUser.UserName ?? "system",
			notes: request?.Notes,
			cancellationToken: cancellationToken);

		_logger.LogInformation(
			"DLQ message acknowledged. Id={DlqId}, AcknowledgedBy={AcknowledgedBy}",
			id,
			CurrentUser.UserName);

		return Ok(new
		{
			success = true,
			message = "Message acknowledged",
			dlqId = id,
			acknowledgedAt = DateTime.UtcNow
		});
	}

	/// <summary>
	/// Update priority of a DLQ message
	/// </summary>
	[HttpPatch("messages/{id:guid}/priority")]
	public async Task<IActionResult> UpdatePriorityAsync(
		Guid id,
		[FromBody] UpdatePriorityRequest request,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Priority))
		{
			return BadRequest(new { success = false, message = "Priority is required" });
		}

		var validPriorities = new[] { DlqPrioritiesConsts.Low, DlqPrioritiesConsts.Normal, DlqPrioritiesConsts.High, DlqPrioritiesConsts.Critical };
		if (!validPriorities.Contains(request.Priority))
		{
			return BadRequest(new
			{
				success = false,
				message = $"Invalid priority. Must be one of: {string.Join(", ", validPriorities)}"
			});
		}

		var message = await _dlqService.GetByIdAsync(id, cancellationToken);

		if (message == null)
		{
			return NotFound(new { success = false, message = $"DLQ message with ID {id} not found" });
		}

		await _dlqService.UpdatePriorityAsync(id, request.Priority, cancellationToken);

		return Ok(new
		{
			success = true,
			message = "Priority updated",
			dlqId = id,
			newPriority = request.Priority
		});
	}
}

/// <summary>
/// Request to acknowledge a DLQ message
/// </summary>
public class AcknowledgeRequest
{
	public string? Notes { get; set; }
}

/// <summary>
/// Request to update DLQ message priority
/// </summary>
public class UpdatePriorityRequest
{
	public string Priority { get; set; } = string.Empty;
}
