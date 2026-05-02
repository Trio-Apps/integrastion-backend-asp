using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
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

    [Queue("orders")]
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

    [Queue("orders")]
    [UnitOfWork]
    public async Task SweepStuckEnqueuedAsync(int olderThanSeconds = 90, int take = 25)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-Math.Max(30, olderThanSeconds));
        var maxRows = Math.Clamp(take, 1, 100);

        TalabatOrderSyncLog[] stuckOrders;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _orderSyncLogRepository.GetQueryableAsync();
            stuckOrders = await queryable
                .Where(x => x.Status == "Enqueued"
                            && x.Attempts == 0
                            && x.ReceivedAt <= cutoff)
                .OrderBy(x => x.ReceivedAt)
                .Take(maxRows)
                .ToArrayAsync();
        }

        if (stuckOrders.Length == 0)
        {
            return;
        }

        _logger.LogWarning(
            "Talabat enqueue watchdog sweep found {Count} stuck orders older than {OlderThanSeconds}s.",
            stuckOrders.Length,
            olderThanSeconds);

        foreach (var orderLog in stuckOrders)
        {
            try
            {
                await RequeueIfStuckAsync(orderLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Talabat enqueue watchdog sweep failed for order log. OrderLogId={OrderLogId}, VendorCode={VendorCode}, OrderCode={OrderCode}",
                    orderLog.Id,
                    orderLog.VendorCode,
                    orderLog.OrderCode);
            }
        }
    }
}
