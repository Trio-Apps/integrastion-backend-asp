using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Domain.Staging;
using OrderXChange.Integrations.Talabat;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatOrderEnqueueWatchdog : ITransientDependency
{
    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderSyncLogRepository;
    private readonly IDistributedEventBus _eventBus;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<TalabatOrderEnqueueWatchdog> _logger;

    public TalabatOrderEnqueueWatchdog(
        IRepository<TalabatOrderSyncLog, Guid> orderSyncLogRepository,
        IDistributedEventBus eventBus,
        ICurrentTenant currentTenant,
        ILogger<TalabatOrderEnqueueWatchdog> logger)
    {
        _orderSyncLogRepository = orderSyncLogRepository;
        _eventBus = eventBus;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task RequeueIfStuckAsync(Guid orderLogId)
    {
        var orderLog = await _orderSyncLogRepository.FindAsync(orderLogId);
        if (orderLog == null)
        {
            return;
        }

        if (!string.Equals(orderLog.Status, "Enqueued", StringComparison.OrdinalIgnoreCase)
            || orderLog.Attempts > 0)
        {
            return;
        }

        using (_currentTenant.Change(orderLog.TenantId))
        {
            var dispatchEvent = new OrderDispatchEto
            {
                CorrelationId = orderLog.CorrelationId ?? Guid.NewGuid().ToString(),
                AccountId = orderLog.FoodicsAccountId,
                FoodicsAccountId = orderLog.FoodicsAccountId,
                VendorCode = orderLog.VendorCode,
                TenantId = orderLog.TenantId,
                OrderLogId = orderLog.Id,
                IdempotencyKey = $"order-watchdog:{orderLog.VendorCode}:{orderLog.Id}:{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                OccurredAt = DateTime.UtcNow
            };

            await _eventBus.PublishAsync(dispatchEvent);

            _logger.LogWarning(
                "Requeued stuck Talabat order dispatch. OrderLogId={OrderLogId}, VendorCode={VendorCode}, OrderCode={OrderCode}, ShortCode={ShortCode}",
                orderLog.Id,
                orderLog.VendorCode,
                orderLog.OrderCode,
                orderLog.ShortCode);
        }
    }
}
