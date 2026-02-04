using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Staging;
using OrderXChange.Integrations.Foodics;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Foodics;

/// <summary>
/// Handler for Dead Letter Queue (DLQ) messages
/// Implements SDD Section 8 - DLQ Strategy
/// Stores failed messages in database for manual replay
/// </summary>
public class MenuSyncFailedEventHandler 
    : IDistributedEventHandler<MenuSyncFailedEto>, 
      ITransientDependency
{
    private readonly DlqService _dlqService;
    private readonly ILogger<MenuSyncFailedEventHandler> _logger;

    public MenuSyncFailedEventHandler(
        DlqService dlqService,
        ILogger<MenuSyncFailedEventHandler> logger)
    {
        _dlqService = dlqService;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(MenuSyncFailedEto eventData)
    {
        // Log the DLQ message for monitoring and alerting
        _logger.LogError(
            "DLQ Message Received: MenuSync permanently failed. " +
            "CorrelationId={CorrelationId}, AccountId={AccountId}, ErrorCode={ErrorCode}, " +
            "ErrorMessage={ErrorMessage}, Attempts={Attempts}, FailureType={FailureType}",
            eventData.CorrelationId,
            eventData.AccountId,
            eventData.ErrorCode,
            eventData.ErrorMessage,
            eventData.Attempts,
            eventData.FailureType);

        // Store in DLQ table for manual replay via management API
        try
        {
            var dlqMessage = await _dlqService.StoreMenuSyncFailedAsync(eventData);
            
            _logger.LogInformation(
                "DLQ message stored successfully. DlqId={DlqId}, CorrelationId={CorrelationId}",
                dlqMessage.Id,
                eventData.CorrelationId);
        }
        catch (System.Exception ex)
        {
            _logger.LogCritical(
                ex,
                "CRITICAL: Failed to store DLQ message in database! CorrelationId={CorrelationId}, Error={Error}",
                eventData.CorrelationId,
                ex.Message);
            
            // Don't rethrow - we've already logged the error
            // The original event is lost if we can't store it
        }
    }
}

