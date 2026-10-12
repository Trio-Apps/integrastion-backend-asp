using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace OrderXChange.BackgroundJobs;

/// <summary>
/// One-shot Hangfire job that backfills the listing columns
/// (CustomerId, CustomerName, CustomerAddress, PaymentMethod, ExpeditionType, Channel,
/// GrandTotal, DiscountTotal) for TalabatOrderSyncLog rows that were received before
/// those columns were added, and re-derives ExpeditionType as TGO/TMP on rows that still
/// hold Talabat's raw "delivery" value (see <see cref="TalabatDeliveryModel"/>).
/// Safe to enqueue multiple times — already-populated rows are skipped by the WHERE filter.
/// Each batch runs in its own short unit of work so a large backlog (hundreds of thousands
/// of rows) neither holds one long transaction nor accumulates tracked entities.
/// </summary>
public class BackfillOrderSyncLogListingFieldsJob : ITransientDependency
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly IDbContextProvider<OrderXChangeDbContext> _dbContextProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<BackfillOrderSyncLogListingFieldsJob> _logger;

    public BackfillOrderSyncLogListingFieldsJob(
        IDbContextProvider<OrderXChangeDbContext> dbContextProvider,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<BackfillOrderSyncLogListingFieldsJob> logger)
    {
        _dbContextProvider = dbContextProvider;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        opts.Converters.Add(new TalabatFlexibleStringJsonConverter());
        return opts;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("BackfillOrderSyncLogListingFields started at {Timestamp}", DateTimeOffset.UtcNow);

        // Collect all IDs across all tenants that still need backfilling.
        // A row needs backfilling when WebhookPayloadJson is present but
        // none of the new listing fields have been written yet, or when its
        // ExpeditionType is still Talabat's raw "delivery" (not yet TGO/TMP).
        List<Guid> pendingIds;
        using (var readUow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            var readContext = await _dbContextProvider.GetDbContextAsync();
            pendingIds = await readContext.TalabatOrderSyncLogs
                .IgnoreQueryFilters()
                .Where(x => x.WebhookPayloadJson != null
                            && ((x.CustomerId == null
                                 && x.CustomerName == null
                                 && x.PaymentMethod == null
                                 && x.GrandTotal == null)
                                || x.ExpeditionType == "delivery"))
                .OrderBy(x => x.CreationTime)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            await readUow.CompleteAsync(cancellationToken);
        }

        var total = pendingIds.Count;
        _logger.LogInformation("Found {Count} TalabatOrderSyncLog rows needing listing-field backfill", total);

        if (total == 0)
        {
            _logger.LogInformation("BackfillOrderSyncLogListingFields: nothing to do");
            return;
        }

        var updated = 0;
        var skipped = 0;

        for (var i = 0; i < pendingIds.Count; i += batchSize)
        {
            var batchIds = pendingIds.GetRange(i, Math.Min(batchSize, pendingIds.Count - i));

            using var batchUow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var dbContext = await _dbContextProvider.GetDbContextAsync();

            var batch = await dbContext.TalabatOrderSyncLogs
                .IgnoreQueryFilters()
                .Where(x => batchIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var row in batch)
            {
                if (string.IsNullOrWhiteSpace(row.WebhookPayloadJson))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var webhook = JsonSerializer.Deserialize<TalabatOrderWebhook>(
                        row.WebhookPayloadJson, JsonOptions);

                    if (webhook == null)
                    {
                        skipped++;
                        continue;
                    }

                    row.CustomerId = webhook.Customer?.Id;
                    row.CustomerName = BuildCustomerName(webhook.Customer);
                    row.CustomerPhone = BuildCustomerPhone(webhook.Customer);
                    row.CustomerAddress = BuildCustomerAddress(webhook.Delivery);
                    row.PaymentMethod = webhook.Payment?.Type;
                    row.ExpeditionType = TalabatDeliveryModel.Resolve(webhook);
                    row.Channel = webhook.LocalInfo?.PlatformKey ?? webhook.LocalInfo?.Platform;
                    row.GrandTotal = ParseDecimal(webhook.Price?.GrandTotal);
                    row.DiscountTotal = ParseDecimal(webhook.Price?.DiscountAmountTotal);

                    updated++;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to deserialize WebhookPayloadJson for TalabatOrderSyncLog {Id} — skipping row",
                        row.Id);
                    skipped++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await batchUow.CompleteAsync(cancellationToken);

            _logger.LogInformation(
                "BackfillOrderSyncLogListingFields batch {BatchEnd}/{Total} saved",
                Math.Min(i + batchSize, total),
                total);
        }

        _logger.LogInformation(
            "BackfillOrderSyncLogListingFields completed: {Updated} updated, {Skipped} skipped, {Total} total",
            updated,
            skipped,
            total);
    }

    private static string? BuildCustomerName(TalabatOrderCustomer? customer)
    {
        if (customer == null) return null;
        var name = string.Join(" ", new[] { customer.FirstName, customer.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;
        return name.Length > 200 ? name[..200] : name;
    }

    private static string? BuildCustomerPhone(TalabatOrderCustomer? customer)
    {
        if (customer == null) return null;
        var parts = new[] { customer.MobilePhoneCountryCode, customer.MobilePhone }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var phone = string.Join("", parts).Trim();
        if (string.IsNullOrWhiteSpace(phone)) return null;
        return phone.Length > 50 ? phone[..50] : phone;
    }

    private static string? BuildCustomerAddress(TalabatOrderDelivery? delivery)
    {
        var address = delivery?.Address;
        if (address == null) return null;
        var combined = string.Join(", ", new[]
            {
                address.Line1, address.Line2, address.Line3,
                address.Street, address.District, address.City
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)).Trim();
        if (string.IsNullOrWhiteSpace(combined)) return null;
        return combined.Length > 500 ? combined[..500] : combined;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}
