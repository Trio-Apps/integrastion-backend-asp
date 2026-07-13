using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Authorization;
using OrderXChange.BackgroundJobs;
using OrderXChange.Domain.Staging;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Integrations.Talabat;
using OrderXChange.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System.Linq.Dynamic.Core;
using Volo.Abp;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.TenantManagement.Talabat;

namespace OrderXChange.Talabat;

[Authorize(OrderXChangePermissions.Orders.Default)]
public class TalabatOrderLogAppService : ApplicationService, ITalabatOrderLogAppService
{
    private static readonly JsonSerializerOptions WebhookJsonOptions = CreateWebhookJsonOptions();

    private static JsonSerializerOptions CreateWebhookJsonOptions()
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        opts.Converters.Add(new TalabatFlexibleStringJsonConverter());
        return opts;
    }

    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderLogRepository;
    private readonly IRepository<TalabatAccount, Guid> _talabatAccountRepository;
    private readonly ICurrentUserBranchProvider _branchProvider;
    private readonly IDistributedEventBus _eventBus;
    private readonly IBackgroundJobClient _backgroundJobs;

    public TalabatOrderLogAppService(
        IRepository<TalabatOrderSyncLog, Guid> orderLogRepository,
        IRepository<TalabatAccount, Guid> talabatAccountRepository,
        ICurrentUserBranchProvider branchProvider,
        IDistributedEventBus eventBus,
        IBackgroundJobClient backgroundJobs)
    {
        _orderLogRepository = orderLogRepository;
        _talabatAccountRepository = talabatAccountRepository;
        _branchProvider = branchProvider;
        _eventBus = eventBus;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<PagedResultDto<TalabatOrderLogDto>> GetListAsync(GetTalabatOrderLogsInput input)
    {
        var scope = await _branchProvider.GetScopeAsync();

        // Fail-closed: a restricted user with no branch grants sees nothing.
        if (!scope.AllBranches && scope.BranchIds.Count == 0)
            return new PagedResultDto<TalabatOrderLogDto>(0, []);

        var queryable = await _orderLogRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        // Branch-scope filter: resolve allowed VendorCodes via TalabatAccount.FoodicsBranchId.
        if (!scope.AllBranches)
        {
            var branchIds = scope.BranchIds;
            var accountQueryable = await _talabatAccountRepository.GetQueryableAsync();
            var allowedVendorCodes = await accountQueryable
                .Where(a => a.FoodicsBranchId != null && branchIds.Contains(a.FoodicsBranchId))
                .Select(a => a.VendorCode)
                .Distinct()
                .ToListAsync();

            queryable = queryable.Where(x => allowedVendorCodes.Contains(x.VendorCode));
        }

        // Explicit branch filter (narrows within the user's scope).
        if (!string.IsNullOrWhiteSpace(input.BranchId))
        {
            var branchId = input.BranchId.Trim();
            var accountQueryable = await _talabatAccountRepository.GetQueryableAsync();
            var branchVendorCodes = await accountQueryable
                .Where(a => a.FoodicsBranchId == branchId)
                .Select(a => a.VendorCode)
                .Distinct()
                .ToListAsync();

            queryable = queryable.Where(x => branchVendorCodes.Contains(x.VendorCode));
        }

        if (!string.IsNullOrWhiteSpace(input.VendorCode))
        {
            var vendor = input.VendorCode.Trim();
            queryable = queryable.Where(x => x.VendorCode == vendor);
        }

        if (!string.IsNullOrWhiteSpace(input.CustomerName))
        {
            var pattern = $"%{input.CustomerName.Trim()}%";
            queryable = queryable.Where(x => x.CustomerName != null && EF.Functions.Like(x.CustomerName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(input.CustomerPhone))
        {
            var pattern = $"%{input.CustomerPhone.Trim()}%";
            queryable = queryable.Where(x => x.CustomerPhone != null && EF.Functions.Like(x.CustomerPhone, pattern));
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

        var maxResultCount = input.MaxResultCount <= 0
            ? 10
            : Math.Min(input.MaxResultCount, 100);

        var items = await queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(maxResultCount)
            .Select(x => new TalabatOrderLogDto
            {
                Id = x.Id,
                FoodicsAccountId = x.FoodicsAccountId,
                VendorCode = x.VendorCode,
                PlatformRestaurantId = x.PlatformRestaurantId,
                OrderToken = x.OrderToken,
                OrderCode = x.OrderCode,
                ShortCode = x.ShortCode,
                Status = x.Status,
                IsTestOrder = x.IsTestOrder,
                ProductsCount = x.ProductsCount,
                CategoriesCount = x.CategoriesCount,
                OrderCreatedAt = x.OrderCreatedAt,
                ReceivedAt = x.ReceivedAt,
                LastAttemptAt = x.LastAttemptUtc,
                Attempts = x.Attempts,
                LastError = x.ErrorMessage,
                CreationTime = x.CreationTime,
                CustomerId = x.CustomerId,
                CustomerName = x.CustomerName,
                CustomerPhone = x.CustomerPhone,
                CustomerAddress = x.CustomerAddress,
                PaymentMethod = x.PaymentMethod,
                ExpeditionType = x.ExpeditionType,
                Channel = x.Channel,
                GrandTotal = x.GrandTotal,
                DiscountTotal = x.DiscountTotal
            })
            .ToListAsync();

        return new PagedResultDto<TalabatOrderLogDto>(totalCount, items);
    }

    public async Task<TalabatOrderDetailsDto> GetDetailsAsync(Guid id)
    {
        var log = await _orderLogRepository.GetAsync(id);

        var scope = await _branchProvider.GetScopeAsync();
        if (!scope.AllBranches)
        {
            if (scope.BranchIds.Count == 0)
                throw new BusinessException("ORDER_ACCESS_DENIED").WithData("OrderLogId", id);

            var accountQueryable = await _talabatAccountRepository.GetQueryableAsync();
            var branchIds = scope.BranchIds;
            var allowed = await accountQueryable
                .Where(a => a.FoodicsBranchId != null && branchIds.Contains(a.FoodicsBranchId))
                .Select(a => a.VendorCode)
                .Distinct()
                .ToListAsync();

            if (!allowed.Contains(log.VendorCode))
                throw new BusinessException("ORDER_ACCESS_DENIED").WithData("OrderLogId", id);
        }

        TalabatOrderWebhook? webhook = null;
        if (!string.IsNullOrWhiteSpace(log.WebhookPayloadJson))
        {
            try
            {
                webhook = JsonSerializer.Deserialize<TalabatOrderWebhook>(
                    log.WebhookPayloadJson, WebhookJsonOptions);
            }
            catch (JsonException)
            {
                // payload unparseable — still return the log metadata, items will be empty
            }
        }

        var dto = new TalabatOrderDetailsDto
        {
            Id = log.Id,
            OrderCode = log.OrderCode,
            OrderToken = log.OrderToken,
            ShortCode = log.ShortCode,
            VendorCode = log.VendorCode,
            Status = log.Status,
            OrderCreatedAt = log.OrderCreatedAt,
            ReceivedAt = log.ReceivedAt,
            CustomerId = log.CustomerId,
            CustomerName = log.CustomerName,
            CustomerPhone = log.CustomerPhone,
            CustomerAddress = log.CustomerAddress,
            PaymentMethod = log.PaymentMethod,
            ExpeditionType = log.ExpeditionType,
            Channel = log.Channel,
            GrandTotal = log.GrandTotal,
            DiscountTotal = log.DiscountTotal,
            CustomerComment = webhook?.Comments?.CustomerComment,
            Items = webhook?.Products?.Select(MapProduct).ToList() ?? [],
            LastError = log.ErrorMessage,
            Attempts = log.Attempts,
            FoodicsOrderId = log.FoodicsOrderId,
            RawPayloadJson = log.WebhookPayloadJson,
        };

        return dto;
    }

    private static TalabatOrderItemDto MapProduct(TalabatOrderProduct p)
    {
        var discounts = p.Discounts?.Select(MapDiscount).ToList() ?? [];
        return new TalabatOrderItemDto
        {
            Name = p.Name,
            CategoryName = p.CategoryName,
            RemoteCode = p.RemoteCode,
            Quantity = ParseInt(p.Quantity),
            UnitPrice = ParseDecimal(p.UnitPrice),
            PaidPrice = ParseDecimal(p.PaidPrice),
            DiscountAmount = discounts.Count > 0
                ? discounts.Sum(d => d.Amount ?? 0m)
                : ParseDecimal(p.DiscountAmount),
            Discounts = discounts,
            Modifiers = p.SelectedToppings?.Select(MapTopping).ToList() ?? []
        };
    }

    private static TalabatOrderModifierDto MapTopping(TalabatOrderTopping t)
    {
        var discounts = t.Discounts?.Select(MapDiscount).ToList() ?? [];
        return new TalabatOrderModifierDto
        {
            Name = t.Name,
            RemoteCode = t.RemoteCode,
            Quantity = t.Quantity ?? 1,
            Price = ParseDecimal(t.Price),
            DiscountAmount = discounts.Count > 0 ? discounts.Sum(d => d.Amount ?? 0m) : null,
            Discounts = discounts
        };
    }

    private static TalabatOrderItemDiscountDto MapDiscount(TalabatOrderDiscount d) =>
        new()
        {
            Name = d.Name,
            Amount = ParseDecimal(d.Amount),
            Sponsorships = d.Sponsorships?.Select(s => new TalabatOrderDiscountSponsorshipDto
            {
                Sponsor = s.Sponsor,
                Amount = ParseDecimal(s.Amount)
            }).ToList() ?? []
        };

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static int ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        return int.TryParse(value, out var result) ? result : 0;
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

    public async Task<List<BranchLookupDto>> GetAccessibleBranchesAsync()
    {
        var scope = await _branchProvider.GetScopeAsync();

        if (!scope.AllBranches && scope.BranchIds.Count == 0)
            return [];

        var accountQueryable = await _talabatAccountRepository.GetQueryableAsync();
        accountQueryable = accountQueryable.AsNoTracking();

        if (!scope.AllBranches)
        {
            var branchIds = scope.BranchIds;
            accountQueryable = accountQueryable
                .Where(a => a.FoodicsBranchId != null && branchIds.Contains(a.FoodicsBranchId));
        }

        return await accountQueryable
            .Where(a => a.FoodicsBranchId != null)
            .GroupBy(a => a.FoodicsBranchId)
            .Select(g => new BranchLookupDto
            {
                BranchId = g.Key!,
                BranchName = g.Max(a => a.FoodicsBranchName) ?? g.Key!
            })
            .OrderBy(b => b.BranchName)
            .ToListAsync();
    }

    public Task<string> EnqueueBackfillListingFieldsAsync()
    {
        var jobId = _backgroundJobs.Enqueue<BackfillOrderSyncLogListingFieldsJob>(
            job => job.ExecuteAsync(100, default));
        return Task.FromResult(jobId);
    }
}
