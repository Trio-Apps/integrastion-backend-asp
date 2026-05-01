using System;
using System.Collections.Generic;
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

        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var pattern = $"%{input.SearchTerm.Trim()}%";
            queryable = queryable.Where(x =>
                (x.OrderCode != null && EF.Functions.Like(x.OrderCode, pattern))
                || (x.ShortCode != null && EF.Functions.Like(x.ShortCode, pattern))
                || (x.OrderToken != null && EF.Functions.Like(x.OrderToken, pattern))
                || (x.VendorCode != null && EF.Functions.Like(x.VendorCode, pattern))
                || (x.PlatformRestaurantId != null && EF.Functions.Like(x.PlatformRestaurantId, pattern))
                || (x.Status != null && EF.Functions.Like(x.Status, pattern))
                || (x.ErrorCode != null && EF.Functions.Like(x.ErrorCode, pattern))
                || (x.ErrorMessage != null && EF.Functions.Like(x.ErrorMessage, pattern))
                || (x.FoodicsOrderId != null && EF.Functions.Like(x.FoodicsOrderId, pattern)));
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

        var isRetryable = string.Equals(log.Status, "Failed", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(log.Status, "Enqueued", StringComparison.OrdinalIgnoreCase);

        if (!isRetryable)
        {
            throw new BusinessException("ORDER_NOT_RETRYABLE")
                .WithData("OrderLogId", id)
                .WithData("Status", log.Status);
        }

        await QueueRetryAsync(log);
    }

    public async Task<RetryTalabatOrderLogsResultDto> RetryFailedAndEnqueuedAsync(RetryTalabatOrderLogsInput input)
    {
        var queryable = await _orderLogRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.VendorCode))
        {
            var vendor = input.VendorCode.Trim();
            queryable = queryable.Where(x => x.VendorCode == vendor);
        }

        var retryableStatuses = new List<string> { "Failed" };
        if (input.IncludeEnqueued)
        {
            retryableStatuses.Add("Enqueued");
        }

        var logs = await queryable
            .Where(x => retryableStatuses.Contains(x.Status))
            .OrderBy(x => x.ReceivedAt)
            .ToListAsync();

        var result = new RetryTalabatOrderLogsResultDto();
        foreach (var log in logs)
        {
            if (string.IsNullOrWhiteSpace(log.WebhookPayloadJson))
            {
                result.SkippedCount++;
                continue;
            }

            await QueueRetryAsync(log);
            result.QueuedCount++;
        }

        return result;
    }

    private async Task QueueRetryAsync(TalabatOrderSyncLog log)
    {
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
            IdempotencyKey = $"order-retry:{log.VendorCode}:{log.Id}:{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            OccurredAt = DateTime.UtcNow
        };

        await _eventBus.PublishAsync(retryEvent);
    }
}
