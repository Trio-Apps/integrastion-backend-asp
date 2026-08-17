using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Application.Versioning;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Availability;

/// <summary>
/// Pushes item availability changes to Talabat's catalog so an item marked out of stock in the
/// dashboard actually becomes unavailable on Talabat. Resolves each Foodics product's stable
/// Talabat remote code via the menu mapping before calling the catalog client.
/// </summary>
public class TalabatAvailabilityPushService : ITransientDependency
{
    private readonly IMenuMappingService _menuMappingService;
    private readonly TalabatCatalogClient _catalogClient;
    private readonly ILogger<TalabatAvailabilityPushService> _logger;

    public TalabatAvailabilityPushService(
        IMenuMappingService menuMappingService,
        TalabatCatalogClient catalogClient,
        ILogger<TalabatAvailabilityPushService> logger)
    {
        _menuMappingService = menuMappingService;
        _catalogClient = catalogClient;
        _logger = logger;
    }

    public async Task PushAsync(
        Guid foodicsAccountId,
        string? branchId,
        string vendorCode,
        string? chainCode,
        string? posVendorId,
        IReadOnlyCollection<string> foodicsProductIds,
        bool isAvailable,
        DateTime? availableAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chainCode) || string.IsNullOrWhiteSpace(posVendorId))
        {
            _logger.LogWarning(
                "Skipping Talabat availability push: vendor {VendorCode} is missing ChainCode/PlatformRestaurantId.",
                vendorCode);
            return;
        }

        var items = new List<TalabatItemAvailability>();
        foreach (var productId in foodicsProductIds.Distinct())
        {
            var mapping = await _menuMappingService.GetMappingByFoodicsIdAsync(
                foodicsAccountId, branchId, MenuMappingEntityType.Product, productId, cancellationToken);

            if (mapping == null || string.IsNullOrWhiteSpace(mapping.TalabatRemoteCode))
            {
                _logger.LogWarning(
                    "No Talabat remote code for product {ProductId} (account {AccountId}, branch {BranchId}); skipping availability push for it.",
                    productId, foodicsAccountId, branchId ?? "ALL");
                continue;
            }

            items.Add(new TalabatItemAvailability
            {
                RemoteCode = mapping.TalabatRemoteCode,
                IsAvailable = isAvailable,
                AvailableAt = isAvailable ? null : availableAtUtc
            });
        }

        if (items.Count == 0)
        {
            return;
        }

        var response = await _catalogClient.UpdateCatalogItemAvailabilityAsync(
            chainCode,
            posVendorId,
            vendorCode,
            new TalabatUpdateItemAvailabilityRequest { Items = items },
            cancellationToken);

        if (response is { Success: false })
        {
            _logger.LogWarning(
                "Talabat availability push reported failure for vendor {VendorCode}: {Message}",
                vendorCode, response.Message);
        }
    }
}
