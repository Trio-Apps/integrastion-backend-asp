using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Domain.Staging;
using OrderXChange.Integrations.Talabat;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System.Linq.Dynamic.Core;
using Volo.Abp;
using Volo.Abp.EventBus.Distributed;

namespace OrderXChange.Talabat;

public class TalabatOrderLogAppService : ApplicationService, ITalabatOrderLogAppService
{
    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderLogRepository;
    private readonly IDistributedEventBus _eventBus;

    public TalabatOrderLogAppService(
        IRepository<TalabatOrderSyncLog, Guid> orderLogRepository,
        IDistributedEventBus eventBus)
    {
        _orderLogRepository = orderLogRepository;
        _eventBus = eventBus;
    }

    public async Task<PagedResultDto<TalabatOrderLogDto>> GetListAsync(GetTalabatOrderLogsInput input)
    {
        var queryable = await _orderLogRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.VendorCode))
        {
            var vendor = input.VendorCode.Trim();
            queryable = queryable.Where(x => x.VendorCode == vendor);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = input.Status.Trim();
            queryable = queryable.Where(x => x.Status == status);
        }

        if (input.IsTestOrder.HasValue)
        {
            queryable = queryable.Where(x => x.IsTestOrder == input.IsTestOrder.Value);
        }

        if (input.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.ReceivedAt >= input.FromDate.Value);
        }

        if (input.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.ReceivedAt <= input.ToDate.Value);
        }

        var totalCount = await queryable.CountAsync();

        var sorting = string.IsNullOrWhiteSpace(input.Sorting)
            ? "ReceivedAt desc"
            : input.Sorting;

        var items = await queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        var dtoItems = items.Select(ObjectMapper.Map<TalabatOrderSyncLog, TalabatOrderLogDto>).ToList();

        return new PagedResultDto<TalabatOrderLogDto>(totalCount, dtoItems);
    }

    public async Task RetryAsync(Guid id)
    {
        var log = await _orderLogRepository.GetAsync(id);

        if (string.Equals(log.Status, "Processing", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("ORDER_RETRY_IN_PROGRESS")
                .WithData("OrderLogId", id);
        }

        if (string.Equals(log.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("ORDER_ALREADY_SUCCEEDED")
                .WithData("OrderLogId", id);
        }

        if (!string.Equals(log.Status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("ORDER_NOT_FAILED")
                .WithData("OrderLogId", id)
                .WithData("Status", log.Status);
        }

        if (!string.IsNullOrWhiteSpace(log.ErrorMessage)
            && (log.ErrorMessage.Contains("Idempotency", StringComparison.OrdinalIgnoreCase)
                || log.ErrorMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("ORDER_DUPLICATE_TOKEN")
                .WithData("OrderLogId", id);
        }

        log.Status = "Enqueued";
        log.ErrorMessage = null;
        log.ErrorCode = null;
        log.CompletedAt = null;
        log.LastAttemptUtc = null;
        log.Attempts = 0;

        await _orderLogRepository.UpdateAsync(log, autoSave: true);

        var retryEvent = new OrderDispatchEto
        {
            CorrelationId = Guid.NewGuid().ToString(),
            AccountId = log.FoodicsAccountId,
            FoodicsAccountId = log.FoodicsAccountId,
            VendorCode = log.VendorCode,
            TenantId = log.TenantId,
            OrderLogId = log.Id,
            IdempotencyKey = $"order-retry:{log.VendorCode}:{log.Id}:{DateTime.UtcNow:yyyyMMddHHmmss}",
            OccurredAt = DateTime.UtcNow
        };

        await _eventBus.PublishAsync(retryEvent);
    }
}
