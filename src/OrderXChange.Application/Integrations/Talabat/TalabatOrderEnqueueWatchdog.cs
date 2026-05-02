using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Domain.Staging;
using OrderXChange.Integrations.Talabat;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatOrderEnqueueWatchdog : ITransientDependency
{
    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderSyncLogRepository;
    private readonly OrderDispatchDistributedEventHandler _orderDispatchHandler;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<TalabatOrderEnqueueWatchdog> _logger;

    public TalabatOrderEnqueueWatchdog(
        IRepository<TalabatOrderSyncLog, Guid> orderSyncLogRepository,
        OrderDispatchDistributedEventHandler orderDispatchHandler,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        ILogger<TalabatOrderEnqueueWatchdog> logger)
    {
        _orderSyncLogRepository = orderSyncLogRepository;
        _orderDispatchHandler = orderDispatchHandler;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task RequeueIfStuckAsync(Guid orderLogId)
    {
        TalabatOrderSyncLog? orderLog;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            orderLog = await _orderSyncLogRepository.FindAsync(orderLogId);
        }

        if (orderLog == null)
        {
            _logger.LogWarning(
                "Talabat order enqueue watchdog could not find order log. OrderLogId={OrderLogId}",
                orderLogId);
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

            await _orderDispatchHandler.ProcessWatchdogDispatchAsync(dispatchEvent);

            _logger.LogWarning(
                "Watchdog recovered stuck Talabat order dispatch. OrderLogId={OrderLogId}, VendorCode={VendorCode}, OrderCode={OrderCode}, ShortCode={ShortCode}",
                orderLog.Id,
                orderLog.VendorCode,
                orderLog.OrderCode,
                orderLog.ShortCode);
        }
    }
}
