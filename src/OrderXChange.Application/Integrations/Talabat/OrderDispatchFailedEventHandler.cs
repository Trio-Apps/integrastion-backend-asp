using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Staging;
using OrderXChange.Domain.Staging;
using OrderXChange.Integrations.Talabat;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Talabat;

public class OrderDispatchFailedEventHandler
    : IDistributedEventHandler<OrderDispatchFailedEto>,
      ITransientDependency
{
    private readonly DlqService _dlqService;
    private readonly ILogger<OrderDispatchFailedEventHandler> _logger;

    public OrderDispatchFailedEventHandler(
        DlqService dlqService,
        ILogger<OrderDispatchFailedEventHandler> logger)
    {
        _dlqService = dlqService;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task HandleEventAsync(OrderDispatchFailedEto eventData)
    {
        _logger.LogError(
            "DLQ Message Received: Order dispatch permanently failed. CorrelationId={CorrelationId}, AccountId={AccountId}, ErrorCode={ErrorCode}, Attempts={Attempts}",
            eventData.CorrelationId,
            eventData.AccountId,
            eventData.ErrorCode,
            eventData.Attempts);

        try
        {
            await _dlqService.StoreFailedEventAsync(
                DlqEventTypes.OrderSync,
                eventData.CorrelationId,
                eventData.AccountId,
                eventData.OriginalMessage,
                new System.Exception(eventData.ErrorMessage),
                eventData.Attempts,
                eventData.FailureType,
                eventData.FirstAttemptUtc);

            _logger.LogInformation(
                "Order dispatch DLQ message stored. CorrelationId={CorrelationId}",
                eventData.CorrelationId);
        }
        catch (System.Exception ex)
        {
            _logger.LogCritical(
                ex,
                "CRITICAL: Failed to store Order dispatch DLQ message. CorrelationId={CorrelationId}",
                eventData.CorrelationId);
        }
    }
}
