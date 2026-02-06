using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Idempotency;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Staging;
using OrderXChange.Integrations.Talabat;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Talabat;

public class OrderDispatchDistributedEventHandler
    : IDistributedEventHandler<OrderDispatchEto>,
      IDistributedEventHandler<OrderDispatchRetryEto>,
      ITransientDependency
{
    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderSyncLogRepository;
    private readonly TalabatAccountService _talabatAccountService;
    private readonly TalabatOrderToFoodicsMapper _orderMapper;
    private readonly FoodicsOrderClient _foodicsOrderClient;
    private readonly FoodicsAccountTokenService _tokenService;
    private readonly IdempotencyService _idempotencyService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedEventBus _eventBus;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<OrderDispatchDistributedEventHandler> _logger;

    public OrderDispatchDistributedEventHandler(
        IRepository<TalabatOrderSyncLog, Guid> orderSyncLogRepository,
        TalabatAccountService talabatAccountService,
        TalabatOrderToFoodicsMapper orderMapper,
        FoodicsOrderClient foodicsOrderClient,
        FoodicsAccountTokenService tokenService,
        IdempotencyService idempotencyService,
        ICurrentTenant currentTenant,
        IDistributedEventBus eventBus,
        IBackgroundJobClient backgroundJobs,
        ILogger<OrderDispatchDistributedEventHandler> logger)
    {
        _orderSyncLogRepository = orderSyncLogRepository;
        _talabatAccountService = talabatAccountService;
        _orderMapper = orderMapper;
        _foodicsOrderClient = foodicsOrderClient;
        _tokenService = tokenService;
        _idempotencyService = idempotencyService;
        _currentTenant = currentTenant;
        _eventBus = eventBus;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task HandleEventAsync(OrderDispatchEto eventData)
    {
        _logger.LogInformation(
            "Kafka Consumer (main): Received OrderDispatch event. CorrelationId={CorrelationId}, OrderLogId={OrderLogId}",
            eventData.CorrelationId,
            eventData.OrderLogId);

        await ProcessOrderAsync(eventData, currentAttempt: 1);
    }

    [UnitOfWork]
    public async Task HandleEventAsync(OrderDispatchRetryEto eventData)
    {
        _logger.LogInformation(
            "Kafka Consumer (retry): Received OrderDispatch retry event. CorrelationId={CorrelationId}, Attempts={Attempts}",
            eventData.Message.CorrelationId,
            eventData.Attempts);

        await ProcessOrderAsync(eventData.Message, currentAttempt: eventData.Attempts + 1);
    }

    private async Task ProcessOrderAsync(OrderDispatchEto eventData, int currentAttempt)
    {
        const int maxAttempts = 3;

        try
        {
            var (canProcess, existingRecord) = await _idempotencyService.CheckAndMarkStartedAsync(
                eventData.AccountId,
                eventData.IdempotencyKey);

            if (!canProcess)
            {
                _logger.LogInformation(
                    "Skipping duplicate OrderDispatch request. CorrelationId={CorrelationId}, Status={Status}",
                    eventData.CorrelationId,
                    existingRecord?.Status);
                return;
            }

            using (_currentTenant.Change(eventData.TenantId))
            {
                var orderLog = await _orderSyncLogRepository.GetAsync(eventData.OrderLogId);
                orderLog.Status = "Processing";
                orderLog.Attempts = currentAttempt;
                orderLog.LastAttemptUtc = DateTime.UtcNow;
                await _orderSyncLogRepository.UpdateAsync(orderLog, autoSave: true);

                if (string.IsNullOrWhiteSpace(orderLog.WebhookPayloadJson))
                {
                    throw new InvalidOperationException("Order payload is missing from log. Enable order payload persistence.");
                }

                var webhook = JsonSerializer.Deserialize<TalabatOrderWebhook>(orderLog.WebhookPayloadJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (webhook == null)
                {
                    throw new InvalidOperationException("Failed to deserialize Talabat order payload.");
                }

                var account = await _talabatAccountService.GetAccountByVendorCodeAsync(eventData.VendorCode);
                if (account == null)
                {
                    throw new InvalidOperationException($"TalabatAccount not found for vendor {eventData.VendorCode}.");
                }

                if (string.IsNullOrWhiteSpace(account.FoodicsBranchId))
                {
                    throw new InvalidOperationException(
                        $"Foodics branch is not configured for vendor {eventData.VendorCode}. Configure FoodicsBranchId.");
                }

                var request = _orderMapper.MapToCreateOrder(webhook, account.FoodicsBranchId, account.VendorCode);
                var accessToken = await _tokenService.GetAccessTokenAsync(eventData.FoodicsAccountId);

                var response = await _foodicsOrderClient.CreateOrderAsync(request, accessToken);

                orderLog.Status = "Succeeded";
                orderLog.CompletedAt = DateTime.UtcNow;
                orderLog.LastAttemptUtc = DateTime.UtcNow;
                orderLog.FoodicsOrderId = response?.Id;
                orderLog.FoodicsResponseJson = response == null
                    ? null
                    : JsonSerializer.Serialize(response);

                await _orderSyncLogRepository.UpdateAsync(orderLog, autoSave: true);

                await _idempotencyService.MarkSucceededAsync(eventData.AccountId, eventData.IdempotencyKey);

                _logger.LogInformation(
                    "Order dispatch succeeded. CorrelationId={CorrelationId}, FoodicsOrderId={FoodicsOrderId}",
                    eventData.CorrelationId,
                    response?.Id);
            }
        }
        catch (BusinessException ex) when (ex.Code == "OPERATION_IN_PROGRESS")
        {
            _logger.LogWarning(
                "Order dispatch already in progress. CorrelationId={CorrelationId}",
                eventData.CorrelationId);
        }
        catch (Exception ex)
        {
            var isTransient = IsTransientError(ex);

            _logger.LogError(
                ex,
                "Order dispatch failed on attempt {Attempt}/{MaxAttempts}. CorrelationId={CorrelationId}, Transient={Transient}",
                currentAttempt,
                maxAttempts,
                eventData.CorrelationId,
                isTransient);

            await TryUpdateOrderLogFailedAsync(eventData.OrderLogId, ex, currentAttempt);

            if (!isTransient || currentAttempt >= maxAttempts)
            {
                await SendToDlqAsync(eventData, ex, currentAttempt, isTransient);
                await _idempotencyService.MarkFailedAsync(eventData.AccountId, eventData.IdempotencyKey);
                return;
            }

            var delaySeconds = GetRetryDelaySeconds(currentAttempt);
            var retryEvent = new OrderDispatchRetryEto
            {
                Message = eventData,
                Attempts = currentAttempt,
                ErrorCode = ex.GetType().Name,
                ErrorMessage = ex.Message,
                LastAttemptUtc = DateTime.UtcNow,
                RetryDelaySeconds = delaySeconds,
                FailureType = "Transient"
            };

            _backgroundJobs.Schedule<OrderDispatchRetryPublisher>(
                publisher => publisher.PublishRetryAsync(retryEvent),
                TimeSpan.FromSeconds(delaySeconds));

            _logger.LogInformation(
                "Scheduled OrderDispatch retry via Hangfire. CorrelationId={CorrelationId}, Attempt={Attempt}, DelaySeconds={DelaySeconds}",
                eventData.CorrelationId,
                currentAttempt + 1,
                delaySeconds);
        }
    }

    private async Task TryUpdateOrderLogFailedAsync(Guid orderLogId, Exception ex, int attempts)
    {
        try
        {
            var log = await _orderSyncLogRepository.GetAsync(orderLogId);
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            log.ErrorCode = ex.GetType().Name;
            log.Attempts = attempts;
            log.LastAttemptUtc = DateTime.UtcNow;
            await _orderSyncLogRepository.UpdateAsync(log, autoSave: true);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to update order log failure status. OrderLogId={OrderLogId}", orderLogId);
        }
    }

    private int GetRetryDelaySeconds(int attempt)
    {
        return attempt switch
        {
            1 => 60,
            2 => 300,
            _ => 900
        };
    }

    private async Task SendToDlqAsync(OrderDispatchEto eventData, Exception ex, int attempts, bool isTransient)
    {
        try
        {
            var dlqEvent = new OrderDispatchFailedEto
            {
                CorrelationId = eventData.CorrelationId,
                AccountId = eventData.AccountId,
                OriginalMessage = JsonSerializer.Serialize(eventData),
                ErrorCode = ex.GetType().Name,
                ErrorMessage = ex.Message,
                Attempts = attempts,
                LastAttemptUtc = DateTime.UtcNow,
                FirstAttemptUtc = eventData.OccurredAt,
                FailureType = isTransient ? "Transient" : "Permanent"
            };

            await _eventBus.PublishAsync(dlqEvent);

            _logger.LogWarning(
                "OrderDispatch message sent to DLQ. CorrelationId={CorrelationId}, ErrorCode={ErrorCode}",
                eventData.CorrelationId,
                dlqEvent.ErrorCode);
        }
        catch (Exception dlqEx)
        {
            _logger.LogError(
                dlqEx,
                "Failed to send OrderDispatch message to DLQ. CorrelationId={CorrelationId}",
                eventData.CorrelationId);
        }
    }

    private static bool IsTransientError(Exception ex)
    {
        if (ex is HttpRequestException || ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ex is TimeoutException
            || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase)
            || (ex.Message.Contains("5") && ex.Message.Contains("error", StringComparison.OrdinalIgnoreCase));
    }
}
