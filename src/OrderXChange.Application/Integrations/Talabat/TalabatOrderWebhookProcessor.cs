using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Domain.Staging;
using OrderXChange.Integrations.Talabat;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatOrderWebhookProcessor : ITransientDependency
{
    private static readonly JsonSerializerOptions TalabatWebhookJsonOptions = CreateTalabatWebhookJsonOptions();

    private readonly IConfiguration _configuration;
    private readonly TalabatAccountService _talabatAccountService;
    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderSyncLogRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<TalabatOrderWebhookProcessor> _logger;

    public TalabatOrderWebhookProcessor(
        IConfiguration configuration,
        TalabatAccountService talabatAccountService,
        IRepository<TalabatOrderSyncLog, Guid> orderSyncLogRepository,
        IBackgroundJobClient backgroundJobs,
        ICurrentTenant currentTenant,
        ILogger<TalabatOrderWebhookProcessor> logger)
    {
        _configuration = configuration;
        _talabatAccountService = talabatAccountService;
        _orderSyncLogRepository = orderSyncLogRepository;
        _backgroundJobs = backgroundJobs;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    private static JsonSerializerOptions CreateTalabatWebhookJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new TalabatFlexibleStringJsonConverter());
        return options;
    }

    [Queue("orders")]
    [UnitOfWork]
    public async Task ProcessAsync(string? vendorCode, string rawBody, string correlationId)
    {
        var webhook = JsonSerializer.Deserialize<TalabatOrderWebhook>(rawBody, TalabatWebhookJsonOptions);
        if (webhook == null)
        {
            _logger.LogWarning(
                "Queued Talabat order webhook could not be parsed. CorrelationId={CorrelationId}",
                correlationId);
            return;
        }

        var resolvedVendorCode = ResolveVendorCode(vendorCode, webhook);
        if (string.IsNullOrWhiteSpace(resolvedVendorCode))
        {
            _logger.LogWarning(
                "Unable to resolve vendor code for queued Talabat order webhook. CorrelationId={CorrelationId}, PlatformRestaurantId={PlatformRestaurantId}",
                correlationId,
                webhook.PlatformRestaurant?.Id);
            return;
        }

        var account = await _talabatAccountService.GetAccountByVendorCodeAsync(resolvedVendorCode, CancellationToken.None);
        if (account == null && !string.IsNullOrWhiteSpace(webhook.PlatformRestaurant?.Id))
        {
            account = await _talabatAccountService.GetAccountByPlatformRestaurantIdAsync(webhook.PlatformRestaurant.Id, CancellationToken.None);
        }

        if (account == null)
        {
            _logger.LogWarning(
                "No TalabatAccount found for queued order webhook. CorrelationId={CorrelationId}, VendorCode={VendorCode}, PlatformRestaurantId={PlatformRestaurantId}",
                correlationId,
                resolvedVendorCode,
                webhook.PlatformRestaurant?.Id);
            return;
        }

        if (!account.FoodicsAccountId.HasValue)
        {
            _logger.LogWarning(
                "TalabatAccount missing FoodicsAccountId for queued order webhook. CorrelationId={CorrelationId}, VendorCode={VendorCode}, TalabatAccountId={TalabatAccountId}",
                correlationId,
                account.VendorCode,
                account.Id);
            return;
        }

        var productsCount = webhook.Products?.Count ?? 0;
        var categoriesCount = webhook.Products == null
            ? 0
            : webhook.Products
                .Select(x => x.CategoryName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

        using (_currentTenant.Change(account.TenantId))
        {
            var existingOrderLog = await FindExistingOrderLogAsync(
                account.FoodicsAccountId.Value,
                account.VendorCode,
                webhook);

            if (existingOrderLog != null && IsActiveOrCompletedOrderLogStatus(existingOrderLog.Status))
            {
                _logger.LogInformation(
                    "Duplicate queued Talabat order webhook ignored because an order log already exists. CorrelationId={CorrelationId}, VendorCode={VendorCode}, ExistingOrderLogId={ExistingOrderLogId}, ExistingStatus={ExistingStatus}, OrderCode={OrderCode}, ShortCode={ShortCode}",
                    correlationId,
                    account.VendorCode,
                    existingOrderLog.Id,
                    existingOrderLog.Status,
                    webhook.Code,
                    webhook.ShortCode);
                return;
            }

            var orderLog = new TalabatOrderSyncLog
            {
                FoodicsAccountId = account.FoodicsAccountId.Value,
                VendorCode = account.VendorCode,
                PlatformRestaurantId = webhook.PlatformRestaurant?.Id,
                OrderToken = webhook.Token,
                OrderCode = webhook.Code,
                ShortCode = webhook.ShortCode,
                CorrelationId = correlationId,
                Status = "Enqueued",
                IsTestOrder = webhook.Test ?? false,
                ProductsCount = productsCount,
                CategoriesCount = categoriesCount,
                OrderCreatedAt = webhook.CreatedAt,
                ReceivedAt = DateTime.UtcNow,
                WebhookPayloadJson = ShouldLogOrderPayload() ? rawBody : null,
                Attempts = 0
            };

            await _orderSyncLogRepository.InsertAsync(orderLog, autoSave: true);

            var dispatchEvent = new OrderDispatchEto
            {
                CorrelationId = correlationId,
                AccountId = account.FoodicsAccountId.Value,
                FoodicsAccountId = account.FoodicsAccountId.Value,
                VendorCode = account.VendorCode,
                TenantId = account.TenantId,
                OrderLogId = orderLog.Id,
                IdempotencyKey = BuildOrderIdempotencyKey(account.VendorCode, webhook),
                OccurredAt = DateTime.UtcNow
            };

            var orderDispatchJobId = _backgroundJobs.Enqueue<OrderDispatchDistributedEventHandler>(
                handler => handler.ProcessHangfireDispatchAsync(dispatchEvent));

            var watchdogDelaySeconds = Math.Max(
                15,
                _configuration.GetValue<int?>("Talabat:OrderEnqueueWatchdogDelaySeconds") ?? 60);

            _backgroundJobs.Schedule<TalabatOrderEnqueueWatchdog>(
                watchdog => watchdog.RequeueIfStuckAsync(orderLog.Id),
                TimeSpan.FromSeconds(watchdogDelaySeconds));

            _logger.LogInformation(
                "Queued Talabat order webhook persisted and dispatched on orders queue. CorrelationId={CorrelationId}, VendorCode={VendorCode}, OrderLogId={OrderLogId}, HangfireJobId={HangfireJobId}, Products={Products}",
                correlationId,
                account.VendorCode,
                orderLog.Id,
                orderDispatchJobId,
                productsCount);
        }
    }

    private bool ShouldLogOrderPayload()
    {
        return _configuration.GetValue<bool?>("Talabat:LogOrderPayload")
               ?? _configuration.GetValue<bool?>("Talabat:LogCatalogPayload")
               ?? true;
    }

    private string? ResolveVendorCode(string? vendorCode, TalabatOrderWebhook webhook)
    {
        if (!string.IsNullOrWhiteSpace(vendorCode))
        {
            return vendorCode;
        }

        var platformRestaurantId = webhook.PlatformRestaurant?.Id;
        if (string.IsNullOrWhiteSpace(platformRestaurantId))
        {
            return null;
        }

        var vendorConfig = _configuration.GetSection("Talabat:VendorConfig").GetChildren();
        foreach (var vendorSection in vendorConfig)
        {
            var configuredRestaurantId = vendorSection.GetValue<string>("PlatformRestaurantId");
            if (string.Equals(configuredRestaurantId, platformRestaurantId, StringComparison.OrdinalIgnoreCase))
            {
                return vendorSection.Key;
            }
        }

        return null;
    }

    private static string BuildOrderIdempotencyKey(string vendorCode, TalabatOrderWebhook webhook)
    {
        var keyPart = !string.IsNullOrWhiteSpace(webhook.Token)
            ? webhook.Token
            : !string.IsNullOrWhiteSpace(webhook.Code)
                ? webhook.Code
                : Guid.NewGuid().ToString("N");

        return $"order:{vendorCode}:{keyPart}";
    }

    private async Task<TalabatOrderSyncLog?> FindExistingOrderLogAsync(
        Guid foodicsAccountId,
        string vendorCode,
        TalabatOrderWebhook webhook)
    {
        var queryable = await _orderSyncLogRepository.GetQueryableAsync();
        var query = queryable.Where(x =>
            x.FoodicsAccountId == foodicsAccountId
            && x.VendorCode == vendorCode);

        if (!string.IsNullOrWhiteSpace(webhook.Token))
        {
            var token = webhook.Token.Trim();
            return await query
                .Where(x => x.OrderToken == token)
                .OrderByDescending(x => x.ReceivedAt)
                .FirstOrDefaultAsync();
        }

        if (!string.IsNullOrWhiteSpace(webhook.Code))
        {
            var code = webhook.Code.Trim();
            return await query
                .Where(x => x.OrderCode == code)
                .OrderByDescending(x => x.ReceivedAt)
                .FirstOrDefaultAsync();
        }

        if (!string.IsNullOrWhiteSpace(webhook.ShortCode))
        {
            var shortCode = webhook.ShortCode.Trim();
            return await query
                .Where(x => x.ShortCode == shortCode)
                .OrderByDescending(x => x.ReceivedAt)
                .FirstOrDefaultAsync();
        }

        return null;
    }

    private static bool IsActiveOrCompletedOrderLogStatus(string? status)
    {
        return string.Equals(status, "Enqueued", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "Processing", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
    }
}
