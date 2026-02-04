using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Staging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service for managing Menu Sync replay workflows
/// Provides orchestrated replay capabilities with validation and rollback
/// </summary>
public class MenuSyncReplayWorkflowService : ITransientDependency
{
    private readonly IMenuSyncDlqService _dlqService;
    private readonly IRepository<DlqMessage, Guid> _dlqRepository;
    private readonly ILogger<MenuSyncReplayWorkflowService> _logger;

    public MenuSyncReplayWorkflowService(
        IMenuSyncDlqService dlqService,
        IRepository<DlqMessage, Guid> dlqRepository,
        ILogger<MenuSyncReplayWorkflowService> logger)
    {
        _dlqService = dlqService;
        _dlqRepository = dlqRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes a comprehensive replay workflow for a specific account
    /// Includes pre-validation, prioritized replay, and post-validation
    /// </summary>
    public async Task<MenuSyncReplayWorkflowResult> ExecuteReplayWorkflowAsync(
        Guid foodicsAccountId,
        string replayedBy,
        MenuSyncReplayWorkflowOptions options,
        CancellationToken cancellationToken = default)
    {
        var workflowResult = new MenuSyncReplayWorkflowResult
        {
            FoodicsAccountId = foodicsAccountId,
            ReplayedBy = replayedBy,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation(
                "Starting replay workflow. AccountId={AccountId}, ReplayedBy={ReplayedBy}",
                foodicsAccountId, replayedBy);

            // Step 1: Get pending messages
            var pendingMessages = await _dlqService.GetPendingMessagesAsync(
                foodicsAccountId, 
                options.EventTypeFilter, 
                options.PriorityFilter, 
                cancellationToken);

            if (!pendingMessages.Any())
            {
                _logger.LogInformation("No pending DLQ messages found for account {AccountId}", foodicsAccountId);
                workflowResult.CompletedAt = DateTime.UtcNow;
                return workflowResult;
            }

            // Step 2: Filter and prioritize messages
            var filteredMessages = FilterMessages(pendingMessages, options);
            var prioritizedMessages = PrioritizeMessages(filteredMessages, options);

            workflowResult.TotalMessages = prioritizedMessages.Count;

            _logger.LogInformation(
                "Found {Total} messages for replay after filtering. AccountId={AccountId}",
                prioritizedMessages.Count, foodicsAccountId);

            // Step 3: Pre-validation
            if (options.EnablePreValidation)
            {
                var preValidationResult = await ValidateMessagesAsync(prioritizedMessages, cancellationToken);
                workflowResult.PreValidationResult = preValidationResult;

                if (!preValidationResult.CanProceed && options.FailOnPreValidation)
                {
                    workflowResult.Status = "Failed";
                    workflowResult.ErrorMessage = "Pre-validation failed";
                    workflowResult.CompletedAt = DateTime.UtcNow;
                    return workflowResult;
                }
            }

            // Step 4: Execute replay in batches
            var batchSize = options.BatchSize;
            var batches = prioritizedMessages
                .Select((msg, index) => new { msg, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.msg.Id).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var batchResult = await _dlqService.ReplayMessagesAsync(batch, replayedBy, cancellationToken);
                workflowResult.BatchResults.Add(batchResult);

                workflowResult.SuccessfulReplays += batchResult.SuccessfulReplays;
                workflowResult.FailedReplays += batchResult.FailedReplays;

                _logger.LogInformation(
                    "Batch replay completed. Success={Success}, Failed={Failed}, Rate={Rate:F1}%",
                    batchResult.SuccessfulReplays, batchResult.FailedReplays, batchResult.SuccessRate);

                // Delay between batches to avoid overwhelming the system
                if (options.BatchDelay > TimeSpan.Zero)
                {
                    await Task.Delay(options.BatchDelay, cancellationToken);
                }
            }

            // Step 5: Post-validation
            if (options.EnablePostValidation)
            {
                var postValidationResult = await ValidateReplayResultsAsync(workflowResult, cancellationToken);
                workflowResult.PostValidationResult = postValidationResult;
            }

            // Step 6: Generate summary
            workflowResult.Status = workflowResult.FailedReplays == 0 ? "Completed" : "PartiallyCompleted";
            workflowResult.SuccessRate = workflowResult.TotalMessages > 0 
                ? (double)workflowResult.SuccessfulReplays / workflowResult.TotalMessages * 100 
                : 0;

            workflowResult.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Replay workflow completed. AccountId={AccountId}, Total={Total}, Success={Success}, Failed={Failed}, Rate={Rate:F1}%",
                foodicsAccountId, workflowResult.TotalMessages, workflowResult.SuccessfulReplays, 
                workflowResult.FailedReplays, workflowResult.SuccessRate);

            return workflowResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replay workflow failed. AccountId={AccountId}", foodicsAccountId);
            
            workflowResult.Status = "Failed";
            workflowResult.ErrorMessage = ex.Message;
            workflowResult.CompletedAt = DateTime.UtcNow;
            
            return workflowResult;
        }
    }

    /// <summary>
    /// Gets replay workflow recommendations based on DLQ analysis
    /// </summary>
    public async Task<MenuSyncReplayRecommendations> GetReplayRecommendationsAsync(
        Guid foodicsAccountId,
        CancellationToken cancellationToken = default)
    {
        var pendingMessages = await _dlqService.GetPendingMessagesAsync(foodicsAccountId, cancellationToken: cancellationToken);
        var statistics = await _dlqService.GetDlqStatisticsAsync(
            foodicsAccountId, 
            DateTime.UtcNow.AddDays(-7), 
            DateTime.UtcNow, 
            cancellationToken);

        var recommendations = new MenuSyncReplayRecommendations
        {
            FoodicsAccountId = foodicsAccountId,
            TotalPendingMessages = pendingMessages.Count,
            RecommendedBatchSize = CalculateOptimalBatchSize(pendingMessages.Count),
            RecommendedPriority = DeterminePriorityStrategy(pendingMessages),
            EstimatedDuration = EstimateReplayDuration(pendingMessages.Count),
            RiskAssessment = AssessReplayRisk(pendingMessages, statistics)
        };

        // Add specific recommendations
        if (pendingMessages.Any(m => m.FailureType == DlqFailureTypes.Transient))
        {
            recommendations.Recommendations.Add("Consider auto-retry for transient failures before manual replay");
        }

        if (pendingMessages.Count(m => m.Priority == DlqPriorities.Critical) > 0)
        {
            recommendations.Recommendations.Add("Prioritize critical messages for immediate replay");
        }

        if (statistics.ReplaySuccessRate < 70)
        {
            recommendations.Recommendations.Add("Low historical success rate - consider investigating root causes first");
        }

        return recommendations;
    }

    #region Private Methods

    private List<DlqMessage> FilterMessages(List<DlqMessage> messages, MenuSyncReplayWorkflowOptions options)
    {
        var filtered = messages.AsEnumerable();

        if (options.MaxAge.HasValue)
        {
            var cutoff = DateTime.UtcNow - options.MaxAge.Value;
            filtered = filtered.Where(m => m.CreationTime >= cutoff);
        }

        if (options.ExcludeEventTypes?.Any() == true)
        {
            filtered = filtered.Where(m => !options.ExcludeEventTypes.Contains(m.EventType));
        }

        if (options.IncludeOnlyTransient)
        {
            filtered = filtered.Where(m => m.FailureType == DlqFailureTypes.Transient);
        }

        return filtered.ToList();
    }

    private List<DlqMessage> PrioritizeMessages(List<DlqMessage> messages, MenuSyncReplayWorkflowOptions options)
    {
        return options.PriorityStrategy switch
        {
            "Critical" => messages.OrderByDescending(m => m.Priority == DlqPriorities.Critical ? 4 :
                                                         m.Priority == DlqPriorities.High ? 3 :
                                                         m.Priority == DlqPriorities.Normal ? 2 : 1)
                                 .ThenBy(m => m.CreationTime).ToList(),
            
            "Chronological" => messages.OrderBy(m => m.CreationTime).ToList(),
            
            "ReverseChronological" => messages.OrderByDescending(m => m.CreationTime).ToList(),
            
            _ => messages.OrderByDescending(m => m.Priority == DlqPriorities.Critical ? 4 :
                                               m.Priority == DlqPriorities.High ? 3 :
                                               m.Priority == DlqPriorities.Normal ? 2 : 1)
                        .ThenBy(m => m.CreationTime).ToList()
        };
    }

    private async Task<MenuSyncReplayValidationResult> ValidateMessagesAsync(
        List<DlqMessage> messages, 
        CancellationToken cancellationToken)
    {
        var result = new MenuSyncReplayValidationResult { CanProceed = true };

        // Check for dependency conflicts
        var deltaMessages = messages.Where(m => m.EventType == MenuSyncDlqEventTypes.DeltaSync).ToList();
        if (deltaMessages.Count > 1)
        {
            result.Warnings.Add($"Multiple delta sync messages found - may cause conflicts");
        }

        // Check message age
        var oldMessages = messages.Where(m => DateTime.UtcNow - m.CreationTime > TimeSpan.FromDays(7)).ToList();
        if (oldMessages.Any())
        {
            result.Warnings.Add($"{oldMessages.Count} messages are older than 7 days - data may be stale");
        }

        // Check for permanent failures
        var permanentFailures = messages.Where(m => m.FailureType == DlqFailureTypes.Permanent).ToList();
        if (permanentFailures.Any())
        {
            result.Warnings.Add($"{permanentFailures.Count} permanent failures - may require manual intervention");
        }

        return result;
    }

    private async Task<MenuSyncReplayValidationResult> ValidateReplayResultsAsync(
        MenuSyncReplayWorkflowResult workflowResult,
        CancellationToken cancellationToken)
    {
        var result = new MenuSyncReplayValidationResult { CanProceed = true };

        if (workflowResult.SuccessRate < 50)
        {
            result.Errors.Add("Low success rate - consider investigating failures");
            result.CanProceed = false;
        }

        if (workflowResult.FailedReplays > workflowResult.SuccessfulReplays)
        {
            result.Warnings.Add("More failures than successes - review error patterns");
        }

        return result;
    }

    private int CalculateOptimalBatchSize(int totalMessages)
    {
        return totalMessages switch
        {
            <= 10 => totalMessages,
            <= 50 => 5,
            <= 100 => 10,
            _ => 20
        };
    }

    private string DeterminePriorityStrategy(List<DlqMessage> messages)
    {
        var criticalCount = messages.Count(m => m.Priority == DlqPriorities.Critical);
        return criticalCount > 0 ? "Critical" : "Chronological";
    }

    private TimeSpan EstimateReplayDuration(int messageCount)
    {
        // Estimate 30 seconds per message on average
        return TimeSpan.FromSeconds(messageCount * 30);
    }

    private string AssessReplayRisk(List<DlqMessage> messages, MenuSyncDlqStatistics statistics)
    {
        var riskScore = 0;

        // Age factor
        if (messages.Any(m => DateTime.UtcNow - m.CreationTime > TimeSpan.FromDays(7)))
            riskScore += 2;

        // Volume factor
        if (messages.Count > 50)
            riskScore += 2;

        // Historical success rate
        if (statistics.ReplaySuccessRate < 70)
            riskScore += 3;

        // Permanent failures
        if (messages.Any(m => m.FailureType == DlqFailureTypes.Permanent))
            riskScore += 1;

        return riskScore switch
        {
            <= 2 => "Low",
            <= 5 => "Medium",
            _ => "High"
        };
    }

    #endregion
}

/// <summary>
/// Options for replay workflow execution
/// </summary>
public class MenuSyncReplayWorkflowOptions
{
    public string? EventTypeFilter { get; set; }
    public string? PriorityFilter { get; set; }
    public TimeSpan? MaxAge { get; set; }
    public List<string>? ExcludeEventTypes { get; set; }
    public bool IncludeOnlyTransient { get; set; }
    public string PriorityStrategy { get; set; } = "Critical";
    public int BatchSize { get; set; } = 10;
    public TimeSpan BatchDelay { get; set; } = TimeSpan.FromSeconds(5);
    public bool EnablePreValidation { get; set; } = true;
    public bool EnablePostValidation { get; set; } = true;
    public bool FailOnPreValidation { get; set; } = false;
}

/// <summary>
/// Result of replay workflow execution
/// </summary>
public class MenuSyncReplayWorkflowResult
{
    public Guid FoodicsAccountId { get; set; }
    public string ReplayedBy { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "InProgress";
    public string? ErrorMessage { get; set; }
    
    public int TotalMessages { get; set; }
    public int SuccessfulReplays { get; set; }
    public int FailedReplays { get; set; }
    public double SuccessRate { get; set; }
    
    public List<MenuSyncDlqBulkReplayResult> BatchResults { get; set; } = new();
    public MenuSyncReplayValidationResult? PreValidationResult { get; set; }
    public MenuSyncReplayValidationResult? PostValidationResult { get; set; }
    
    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
}

/// <summary>
/// Validation result for replay operations
/// </summary>
public class MenuSyncReplayValidationResult
{
    public bool CanProceed { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Recommendations for replay workflow
/// </summary>
public class MenuSyncReplayRecommendations
{
    public Guid FoodicsAccountId { get; set; }
    public int TotalPendingMessages { get; set; }
    public int RecommendedBatchSize { get; set; }
    public string RecommendedPriority { get; set; } = string.Empty;
    public TimeSpan EstimatedDuration { get; set; }
    public string RiskAssessment { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = new();
}