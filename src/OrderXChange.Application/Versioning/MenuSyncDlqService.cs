using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Staging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Implementation of Menu Sync Dead Letter Queue service
/// Handles failed payload storage, manual replay, and DLQ management
/// </summary>
public class MenuSyncDlqService : IMenuSyncDlqService, ITransientDependency
{
    private readonly IRepository<DlqMessage, Guid> _dlqRepository;
    private readonly IMenuDeltaSyncService _deltaSyncService;
    private readonly MenuVersioningService _versioningService;
    private readonly ILogger<MenuSyncDlqService> _logger;

    public MenuSyncDlqService(
        IRepository<DlqMessage, Guid> dlqRepository,
        IMenuDeltaSyncService deltaSyncService,
        MenuVersioningService versioningService,
        ILogger<MenuSyncDlqService> logger)
    {
        _dlqRepository = dlqRepository;
        _deltaSyncService = deltaSyncService;
        _versioningService = versioningService;
        _logger = logger;
    }

    public async Task<Guid> StoreDeltaSyncFailureAsync(
        MenuSyncDlqRequest request,
        CancellationToken cancellationToken = default)
    {
        request.EventType = MenuSyncDlqEventTypes.DeltaSync;
        return await StoreFailureAsync(request, cancellationToken);
    }

    public async Task<Guid> StoreDeltaGenerationFailureAsync(
        MenuSyncDlqRequest request,
        CancellationToken cancellationToken = default)
    {
        request.EventType = MenuSyncDlqEventTypes.DeltaGeneration;
        return await StoreFailureAsync(request, cancellationToken);
    }

    public async Task<Guid> StoreValidationFailureAsync(
        MenuSyncDlqRequest request,
        CancellationToken cancellationToken = default)
    {
        request.EventType = MenuSyncDlqEventTypes.DeltaValidation;
        return await StoreFailureAsync(request, cancellationToken);
    }

    public async Task<MenuSyncDlqReplayResult> ReplayMessageAsync(
        Guid dlqMessageId,
        string replayedBy,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation(
                "Starting DLQ message replay. MessageId={MessageId}, ReplayedBy={ReplayedBy}",
                dlqMessageId, replayedBy);

            var dlqMessage = await _dlqRepository.GetAsync(dlqMessageId, cancellationToken: cancellationToken);
            
            if (dlqMessage.IsReplayed)
            {
                _logger.LogWarning("DLQ message {MessageId} has already been replayed", dlqMessageId);
                return new MenuSyncDlqReplayResult
                {
                    Success = false,
                    DlqMessageId = dlqMessageId,
                    ErrorMessage = "Message has already been replayed",
                    ReplayTime = stopwatch.Elapsed
                };
            }

            // Mark as being replayed
            dlqMessage.IsReplayed = true;
            dlqMessage.ReplayedAt = DateTime.UtcNow;
            dlqMessage.ReplayedBy = replayedBy;
            await _dlqRepository.UpdateAsync(dlqMessage, autoSave: true, cancellationToken: cancellationToken);

            object? operationResult = null;
            
            // Execute the replay based on event type
            switch (dlqMessage.EventType)
            {
                case MenuSyncDlqEventTypes.DeltaSync:
                    operationResult = await ReplayDeltaSyncAsync(dlqMessage, cancellationToken);
                    break;
                    
                case MenuSyncDlqEventTypes.DeltaGeneration:
                    operationResult = await ReplayDeltaGenerationAsync(dlqMessage, cancellationToken);
                    break;
                    
                case MenuSyncDlqEventTypes.DeltaValidation:
                    operationResult = await ReplayDeltaValidationAsync(dlqMessage, cancellationToken);
                    break;
                    
                default:
                    throw new InvalidOperationException($"Unsupported event type for replay: {dlqMessage.EventType}");
            }

            // Update replay result
            dlqMessage.ReplayResult = "Success";
            dlqMessage.ReplayErrorMessage = null;
            await _dlqRepository.UpdateAsync(dlqMessage, autoSave: true, cancellationToken: cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "DLQ message replay completed successfully. MessageId={MessageId}, Time={Time}ms",
                dlqMessageId, stopwatch.ElapsedMilliseconds);

            return new MenuSyncDlqReplayResult
            {
                Success = true,
                DlqMessageId = dlqMessageId,
                ReplayTime = stopwatch.Elapsed,
                OperationResult = operationResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay DLQ message {MessageId}", dlqMessageId);
            
            // Update replay failure
            try
            {
                var dlqMessage = await _dlqRepository.GetAsync(dlqMessageId, cancellationToken: cancellationToken);
                dlqMessage.ReplayResult = "Failed";
                dlqMessage.ReplayErrorMessage = ex.Message;
                await _dlqRepository.UpdateAsync(dlqMessage, autoSave: true, cancellationToken: cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update DLQ message replay failure status");
            }

            return new MenuSyncDlqReplayResult
            {
                Success = false,
                DlqMessageId = dlqMessageId,
                ErrorMessage = ex.Message,
                ReplayTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<MenuSyncDlqBulkReplayResult> ReplayMessagesAsync(
        List<Guid> dlqMessageIds,
        string replayedBy,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<MenuSyncDlqReplayResult>();
        
        _logger.LogInformation(
            "Starting bulk DLQ message replay. Count={Count}, ReplayedBy={ReplayedBy}",
            dlqMessageIds.Count, replayedBy);

        foreach (var messageId in dlqMessageIds)
        {
            try
            {
                var result = await ReplayMessageAsync(messageId, replayedBy, cancellationToken);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replay DLQ message {MessageId} in bulk operation", messageId);
                results.Add(new MenuSyncDlqReplayResult
                {
                    Success = false,
                    DlqMessageId = messageId,
                    ErrorMessage = ex.Message
                });
            }
        }

        stopwatch.Stop();

        var bulkResult = new MenuSyncDlqBulkReplayResult
        {
            TotalAttempted = dlqMessageIds.Count,
            SuccessfulReplays = results.Count(r => r.Success),
            FailedReplays = results.Count(r => !r.Success),
            Results = results,
            TotalTime = stopwatch.Elapsed
        };

        _logger.LogInformation(
            "Bulk DLQ replay completed. Total={Total}, Success={Success}, Failed={Failed}, Rate={Rate:F1}%",
            bulkResult.TotalAttempted, bulkResult.SuccessfulReplays, bulkResult.FailedReplays, bulkResult.SuccessRate);

        return bulkResult;
    }

    public async Task<List<DlqMessage>> GetPendingMessagesAsync(
        Guid foodicsAccountId,
        string? eventType = null,
        string? priority = null,
        CancellationToken cancellationToken = default)
    {
        var query = await _dlqRepository.GetQueryableAsync();
        
        return await query
            .Where(m => m.AccountId == foodicsAccountId)
            .Where(m => !m.IsReplayed && !m.IsAcknowledged)
            .Where(m => eventType == null || m.EventType == eventType)
            .Where(m => priority == null || m.Priority == priority)
            .OrderByDescending(m => m.Priority == DlqPriorities.Critical ? 4 :
                              m.Priority == DlqPriorities.High ? 3 :
                              m.Priority == DlqPriorities.Normal ? 2 : 1)
            .ThenBy(m => m.CreationTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<MenuSyncDlqStatistics> GetDlqStatisticsAsync(
        Guid foodicsAccountId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var query = await _dlqRepository.GetQueryableAsync();
        
        var messages = await query
            .Where(m => m.AccountId == foodicsAccountId)
            .Where(m => m.CreationTime >= fromDate && m.CreationTime <= toDate)
            .ToListAsync(cancellationToken);

        var replayedMessages = messages.Where(m => m.IsReplayed && m.ReplayResult == "Success").ToList();
        var resolutionTimes = messages
            .Where(m => m.IsReplayed || m.IsAcknowledged)
            .Where(m => m.ReplayedAt.HasValue || m.AcknowledgedAt.HasValue)
            .Select(m => 
            {
                var resolutionTime = m.ReplayedAt ?? m.AcknowledgedAt ?? DateTime.UtcNow;
                return (resolutionTime - m.CreationTime).TotalHours;
            })
            .ToList();

        return new MenuSyncDlqStatistics
        {
            FoodicsAccountId = foodicsAccountId,
            FromDate = fromDate,
            ToDate = toDate,
            TotalMessages = messages.Count,
            PendingMessages = messages.Count(m => !m.IsReplayed && !m.IsAcknowledged),
            ReplayedMessages = messages.Count(m => m.IsReplayed),
            AcknowledgedMessages = messages.Count(m => m.IsAcknowledged),
            MessagesByEventType = messages.GroupBy(m => m.EventType).ToDictionary(g => g.Key, g => g.Count()),
            MessagesByFailureType = messages.GroupBy(m => m.FailureType).ToDictionary(g => g.Key, g => g.Count()),
            MessagesByPriority = messages.GroupBy(m => m.Priority).ToDictionary(g => g.Key, g => g.Count()),
            AverageResolutionTimeHours = resolutionTimes.Any() ? resolutionTimes.Average() : 0,
            ReplaySuccessRate = messages.Count(m => m.IsReplayed) > 0 
                ? (double)replayedMessages.Count / messages.Count(m => m.IsReplayed) * 100 
                : 0
        };
    }

    public async Task AcknowledgeMessageAsync(
        Guid dlqMessageId,
        string acknowledgedBy,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var dlqMessage = await _dlqRepository.GetAsync(dlqMessageId, cancellationToken: cancellationToken);
        
        dlqMessage.IsAcknowledged = true;
        dlqMessage.AcknowledgedAt = DateTime.UtcNow;
        dlqMessage.AcknowledgedBy = acknowledgedBy;
        dlqMessage.Notes = notes;
        
        await _dlqRepository.UpdateAsync(dlqMessage, autoSave: true, cancellationToken: cancellationToken);
        
        _logger.LogInformation(
            "DLQ message acknowledged. MessageId={MessageId}, AcknowledgedBy={AcknowledgedBy}",
            dlqMessageId, acknowledgedBy);
    }

    public async Task UpdateMessagePriorityAsync(
        Guid dlqMessageId,
        string priority,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var dlqMessage = await _dlqRepository.GetAsync(dlqMessageId, cancellationToken: cancellationToken);
        
        var oldPriority = dlqMessage.Priority;
        dlqMessage.Priority = priority;
        
        await _dlqRepository.UpdateAsync(dlqMessage, autoSave: true, cancellationToken: cancellationToken);
        
        _logger.LogInformation(
            "DLQ message priority updated. MessageId={MessageId}, OldPriority={OldPriority}, NewPriority={NewPriority}, UpdatedBy={UpdatedBy}",
            dlqMessageId, oldPriority, priority, updatedBy);
    }

    public async Task<MenuSyncDlqCleanupResult> CleanupOldMessagesAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var query = await _dlqRepository.GetQueryableAsync();
            
            var oldMessages = await query
                .Where(m => m.CreationTime < cutoffDate)
                .Where(m => m.IsAcknowledged || (m.IsReplayed && m.ReplayResult == "Success"))
                .ToListAsync(cancellationToken);

            var freedBytes = oldMessages.Sum(m => m.OriginalMessage?.Length ?? 0);
            
            await _dlqRepository.DeleteManyAsync(oldMessages, autoSave: true, cancellationToken: cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "DLQ cleanup completed. Deleted={Count}, FreedKB={Size}, Time={Time}ms",
                oldMessages.Count, freedBytes / 1024, stopwatch.ElapsedMilliseconds);

            return new MenuSyncDlqCleanupResult
            {
                DeletedMessages = oldMessages.Count,
                FreedStorageBytes = freedBytes,
                CleanupTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old DLQ messages");
            
            return new MenuSyncDlqCleanupResult
            {
                CleanupTime = stopwatch.Elapsed,
                Errors = { ex.Message }
            };
        }
    }

    public async Task<MenuSyncDlqAutoRetryResult> AutoRetryTransientFailuresAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var cutoffDate = DateTime.UtcNow - maxAge;
        
        try
        {
            var query = await _dlqRepository.GetQueryableAsync();
            
            var eligibleMessages = await query
                .Where(m => !m.IsReplayed && !m.IsAcknowledged)
                .Where(m => m.FailureType == DlqFailureTypes.Transient)
                .Where(m => m.CreationTime >= cutoffDate)
                .Where(m => m.Attempts < 5) // Max 5 auto-retry attempts
                .OrderBy(m => m.CreationTime)
                .Take(50) // Limit batch size
                .ToListAsync(cancellationToken);

            var retryResults = new List<MenuSyncDlqReplayResult>();
            var successCount = 0;
            var failedCount = 0;
            var exceededLimitCount = 0;

            foreach (var message in eligibleMessages)
            {
                try
                {
                    var result = await ReplayMessageAsync(message.Id, "AutoRetry", cancellationToken);
                    retryResults.Add(result);
                    
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failedCount++;
                        
                        // Update attempt count
                        message.Attempts++;
                        if (message.Attempts >= 5)
                        {
                            message.FailureType = DlqFailureTypes.Permanent;
                            exceededLimitCount++;
                        }
                        await _dlqRepository.UpdateAsync(message, autoSave: true, cancellationToken: cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-retry failed for DLQ message {MessageId}", message.Id);
                    failedCount++;
                }
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "Auto-retry completed. Eligible={Eligible}, Success={Success}, Failed={Failed}, ExceededLimit={ExceededLimit}",
                eligibleMessages.Count, successCount, failedCount, exceededLimitCount);

            return new MenuSyncDlqAutoRetryResult
            {
                EligibleMessages = eligibleMessages.Count,
                SuccessfulRetries = successCount,
                FailedRetries = failedCount,
                ExceededRetryLimit = exceededLimitCount,
                RetryTime = stopwatch.Elapsed,
                RetryResults = retryResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-retry transient failures");
            
            return new MenuSyncDlqAutoRetryResult
            {
                RetryTime = stopwatch.Elapsed
            };
        }
    }

    #region Private Methods

    private async Task<Guid> StoreFailureAsync(
        MenuSyncDlqRequest request,
        CancellationToken cancellationToken)
    {
        var dlqMessage = new DlqMessage
        {
            EventType = request.EventType,
            CorrelationId = request.CorrelationId,
            AccountId = request.FoodicsAccountId,
            OriginalMessage = request.OriginalPayload,
            ErrorCode = request.Exception.GetType().Name,
            ErrorMessage = request.Exception.Message,
            StackTrace = request.Exception.StackTrace,
            Attempts = request.AttemptCount,
            FailureType = request.FailureType,
            FirstAttemptUtc = request.FirstAttemptUtc,
            LastAttemptUtc = DateTime.UtcNow,
            Priority = request.Priority,
            Notes = JsonSerializer.Serialize(request.Context)
        };

        await _dlqRepository.InsertAsync(dlqMessage, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogWarning(
            "Menu sync failure stored in DLQ. EventType={EventType}, CorrelationId={CorrelationId}, Error={Error}",
            request.EventType, request.CorrelationId, request.Exception.Message);

        return dlqMessage.Id;
    }

    private async Task<object> ReplayDeltaSyncAsync(DlqMessage dlqMessage, CancellationToken cancellationToken)
    {
        var context = JsonSerializer.Deserialize<Dictionary<string, object>>(dlqMessage.Notes ?? "{}");
        
        if (!context.TryGetValue("DeltaId", out var deltaIdObj) || 
            !Guid.TryParse(deltaIdObj.ToString(), out var deltaId))
        {
            throw new InvalidOperationException("DeltaId not found in DLQ message context");
        }

        if (!context.TryGetValue("TalabatVendorCode", out var vendorCodeObj) || 
            vendorCodeObj.ToString() is not string vendorCode)
        {
            throw new InvalidOperationException("TalabatVendorCode not found in DLQ message context");
        }

        return await _deltaSyncService.SyncDeltaToTalabatAsync(deltaId, vendorCode, cancellationToken);
    }

    private async Task<object> ReplayDeltaGenerationAsync(DlqMessage dlqMessage, CancellationToken cancellationToken)
    {
        // Parse original payload to extract generation parameters
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(dlqMessage.OriginalMessage);
        
        // This would require reconstructing the original generation request
        // For now, return a placeholder indicating manual intervention needed
        throw new InvalidOperationException("Delta generation replay requires manual intervention - original product data may have changed");
    }

    private async Task<object> ReplayDeltaValidationAsync(DlqMessage dlqMessage, CancellationToken cancellationToken)
    {
        // Parse validation context and retry validation
        var context = JsonSerializer.Deserialize<Dictionary<string, object>>(dlqMessage.Notes ?? "{}");
        
        if (!context.TryGetValue("DeltaId", out var deltaIdObj) || 
            !Guid.TryParse(deltaIdObj.ToString(), out var deltaId))
        {
            throw new InvalidOperationException("DeltaId not found in DLQ message context");
        }

        var payload = await _deltaSyncService.GetDeltaPayloadAsync(deltaId, cancellationToken);
        if (payload == null)
        {
            throw new InvalidOperationException($"Delta payload not found for DeltaId: {deltaId}");
        }

        return await _deltaSyncService.ValidateDeltaPayloadAsync(payload);
    }

    #endregion
}