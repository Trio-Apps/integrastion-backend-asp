using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Hangfire;
using OrderXChange.Application.Idempotency;
using OrderXChange.BackgroundJobs;
using OrderXChange.Integrations.Foodics;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Foodics;

/// <summary>
/// Kafka consumer for Menu Sync events (main + retry topics).
/// Implements SDD Section 9.2 (Menu Sync) and Section 8 (Retry/DLQ) semantics.
/// </summary>
public class MenuSyncDistributedEventHandler 
    : IDistributedEventHandler<MenuSyncEto>,
      IDistributedEventHandler<MenuSyncRetryEto>,
      ITransientDependency
{
    private readonly MenuSyncRecurringJob _menuSyncJob;
    private readonly IdempotencyService _idempotencyService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedEventBus _eventBus;
    private readonly ILogger<MenuSyncDistributedEventHandler> _logger;
    private readonly IBackgroundJobClient _backgroundJobs;

    public MenuSyncDistributedEventHandler(
        MenuSyncRecurringJob menuSyncJob,
        IdempotencyService idempotencyService,
        ICurrentTenant currentTenant,
        IDistributedEventBus eventBus,
        IBackgroundJobClient backgroundJobs,
        ILogger<MenuSyncDistributedEventHandler> logger)
    {
        _menuSyncJob = menuSyncJob;
        _idempotencyService = idempotencyService;
        _currentTenant = currentTenant;
        _eventBus = eventBus;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task HandleEventAsync(MenuSyncEto eventData)
    {
        _logger.LogInformation(
            "Kafka Consumer (main): Received MenuSync event. CorrelationId={CorrelationId}, AccountId={AccountId}, IdempotencyKey={IdempotencyKey}",
            eventData.CorrelationId,
            eventData.AccountId,
            eventData.IdempotencyKey);

        // First attempt comes from the main topic
        await ProcessMenuSyncAsync(eventData, currentAttempt: 1);
    }

    /// <summary>
    /// Handler for retry-topic events. These are scheduled via Hangfire with a delay.
    /// </summary>
    [UnitOfWork]
    public async Task HandleEventAsync(MenuSyncRetryEto eventData)
    {
        _logger.LogInformation(
            "Kafka Consumer (retry): Received MenuSync retry event. CorrelationId={CorrelationId}, Attempts={Attempts}",
            eventData.Message.CorrelationId,
            eventData.Attempts);

        await ProcessMenuSyncAsync(eventData.Message, currentAttempt: eventData.Attempts + 1);
    }

    /// <summary>
    /// Core processing logic shared between main and retry topics.
    /// Does NOT perform in-process Task.Delay; instead, transient failures are
    /// scheduled as new retry events via Hangfire, which maps to logical Kafka
    /// retry topics (menu.sync.retry.1m/5m/15m).
    /// </summary>
    private async Task ProcessMenuSyncAsync(MenuSyncEto eventData, int currentAttempt)
    {
        const int maxAttempts = 3;

        try
        {
            // Check idempotency before processing
            var (canProcess, existingRecord) = await _idempotencyService.CheckAndMarkStartedAsync(
                eventData.AccountId,
                eventData.IdempotencyKey);

            if (!canProcess)
            {
                _logger.LogInformation(
                    "Skipping duplicate MenuSync request. CorrelationId={CorrelationId}, Status={Status}",
                    eventData.CorrelationId,
                    existingRecord?.Status);
                return;
            }

            // Change to correct tenant context
            using (_currentTenant.Change(eventData.TenantId))
            {
                // Execute the actual sync logic
                // Pass skipInternalIdempotency=true because the handler already checked idempotency
                await _menuSyncJob.ExecuteAsync(
                    eventData.FoodicsAccountId,
                    eventData.BranchId,
                    skipInternalIdempotency: true);

                // Mark as succeeded
                await _idempotencyService.MarkSucceededAsync(
                    eventData.AccountId,
                    eventData.IdempotencyKey);

                _logger.LogInformation(
                    "MenuSync completed successfully. CorrelationId={CorrelationId}",
                    eventData.CorrelationId);
            }
        }
        catch (BusinessException ex) when (ex.Code == "OPERATION_IN_PROGRESS")
        {
            _logger.LogWarning(
                "MenuSync operation already in progress. CorrelationId={CorrelationId}",
                eventData.CorrelationId);
            // Do not retry - this is equivalent to HTTP 429 semantics
        }
        catch (Exception ex)
        {
            var isTransient = IsTransientError(ex);

            _logger.LogError(
                ex,
                "MenuSync failed on attempt {Attempt}/{MaxAttempts}. CorrelationId={CorrelationId}, Transient={Transient}, Error={Error}",
                currentAttempt,
                maxAttempts,
                eventData.CorrelationId,
                isTransient,
                ex.Message);

            if (!isTransient || currentAttempt >= maxAttempts)
            {
                // Non-retryable or max attempts reached → DLQ + mark permanent failure
                await SendToDlqAsync(eventData, ex, currentAttempt, isTransient);

                await _idempotencyService.MarkFailedAsync(
                    eventData.AccountId,
                    eventData.IdempotencyKey);

                return;
            }

            // Transient + attempts remaining → schedule retry via Hangfire
            var delaySeconds = GetRetryDelaySeconds(currentAttempt);

            var retryEvent = new MenuSyncRetryEto
            {
                Message = eventData,
                Attempts = currentAttempt,
                ErrorCode = ex.GetType().Name,
                ErrorMessage = ex.Message,
                LastAttemptUtc = DateTime.UtcNow,
                RetryDelaySeconds = delaySeconds,
                FailureType = "Transient"
            };

            _backgroundJobs.Schedule<MenuSyncRetryPublisher>(
                publisher => publisher.PublishRetryAsync(retryEvent),
                TimeSpan.FromSeconds(delaySeconds));

            _logger.LogInformation(
                "Scheduled MenuSync retry via Hangfire. CorrelationId={CorrelationId}, Attempt={Attempt}, DelaySeconds={DelaySeconds}",
                eventData.CorrelationId,
                currentAttempt + 1,
                delaySeconds);
        }
    }

    private int GetRetryDelaySeconds(int attempt)
    {
        // Exponential backoff buckets as per SDD suggestion: 1m, 5m, 15m
        return attempt switch
        {
            1 => 60,    // 1 minute
            2 => 300,   // 5 minutes
            _ => 900    // 15 minutes
        };
    }

    private async Task SendToDlqAsync(MenuSyncEto eventData, Exception ex, int attempts, bool isTransient)
    {
        try
        {
            var dlqEvent = new MenuSyncFailedEto
            {
                CorrelationId = eventData.CorrelationId,
                AccountId = eventData.AccountId,
                OriginalMessage = System.Text.Json.JsonSerializer.Serialize(eventData),
                ErrorCode = ex.GetType().Name,
                ErrorMessage = ex.Message,
                Attempts = attempts,
                LastAttemptUtc = DateTime.UtcNow,
                FirstAttemptUtc = eventData.OccurredAt,
                FailureType = isTransient ? "Transient" : "Permanent"
            };

            await _eventBus.PublishAsync(dlqEvent);

            _logger.LogWarning(
                "MenuSync message sent to DLQ. CorrelationId={CorrelationId}, ErrorCode={ErrorCode}",
                eventData.CorrelationId,
                dlqEvent.ErrorCode);
        }
        catch (Exception dlqEx)
        {
            _logger.LogError(
                dlqEx,
                "Failed to send message to DLQ. CorrelationId={CorrelationId}",
                eventData.CorrelationId);
        }
    }

    private bool IsTransientError(Exception ex)
    {
        // SDD Section 8.1 - Classification
        
        // Permanent errors (should go to DLQ immediately)
        // 401 Unauthorized, 403 Forbidden, 404 Not Found are permanent
        if (ex is HttpRequestException || ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase) 
            || ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
        {
            return false; // Permanent error - send to DLQ
        }
        
        // Transient errors (should retry)
        return ex is TimeoutException 
            || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase)
            || (ex.Message.Contains("5") && ex.Message.Contains("error", StringComparison.OrdinalIgnoreCase));
    }
}

