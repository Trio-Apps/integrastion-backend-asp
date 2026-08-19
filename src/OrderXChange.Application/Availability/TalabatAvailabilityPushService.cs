using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatAvailabilityPushService> _logger;

    public TalabatAvailabilityPushService(
        IMenuMappingService menuMappingService,
        TalabatCatalogClient catalogClient,
        IConfiguration configuration,
        ILogger<TalabatAvailabilityPushService> logger)
    {
        _menuMappingService = menuMappingService;
        _catalogClient = catalogClient;
        _configuration = configuration;
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
        string entityType = AvailabilityEntityType.Product,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chainCode) || string.IsNullOrWhiteSpace(posVendorId))
        {
            _logger.LogWarning(
                "Skipping Talabat availability push: vendor {VendorCode} is missing ChainCode/PlatformRestaurantId.",
                vendorCode);
            return;
        }

        // A topping is a Talabat "TOPPING" and maps through the Modifier mapping; a menu item is
        // an "ITEM" and maps through the Product mapping.
        var isModifier = entityType == AvailabilityEntityType.Modifier;
        var mappingType = isModifier ? MenuMappingEntityType.Modifier : MenuMappingEntityType.Product;

        var remoteCodes = new List<string>();
        foreach (var productId in foodicsProductIds.Distinct())
        {
            var mapping = await _menuMappingService.GetMappingByFoodicsIdAsync(
                foodicsAccountId, branchId, mappingType, productId, cancellationToken);

            if (mapping == null || string.IsNullOrWhiteSpace(mapping.TalabatRemoteCode))
            {
                _logger.LogWarning(
                    "No Talabat remote code for {EntityType} {ProductId} (account {AccountId}, branch {BranchId}); skipping availability push for it.",
                    entityType, productId, foodicsAccountId, branchId ?? "ALL");
                continue;
            }

            remoteCodes.Add(mapping.TalabatRemoteCode);
        }

        if (remoteCodes.Count == 0)
        {
            return;
        }

        var request = new TalabatUpdateItemAvailabilityRequest
        {
            GlobalEntityId = ResolvePlatformKey(vendorCode),
            Items = remoteCodes,
            Type = isModifier ? "TOPPING" : "ITEM",
            IsAvailable = isAvailable,
        };

        // When taking an item out of stock with a known restore time, tell Talabat when it
        // comes back so it auto-restores; otherwise leave it unavailable indefinitely.
        if (!isAvailable && availableAtUtc.HasValue)
        {
            request.WillBeAvailable = "AT_TIMESTAMP";
            request.AtTimeStamp = DateTime.SpecifyKind(availableAtUtc.Value, DateTimeKind.Utc);
        }

        var response = await _catalogClient.UpdateCatalogItemAvailabilityAsync(
            chainCode,
            posVendorId,
            vendorCode,
            request,
            cancellationToken);

        if (response is { Success: false })
        {
            _logger.LogWarning(
                "Talabat availability push reported failure for vendor {VendorCode}: {Message}",
                vendorCode, response.Message);
        }
    }

    // globalEntityId identifies the delivery platform (e.g. "TB_KW" for Talabat Kuwait).
    // Prefer a per-vendor override, then the top-level Talabat setting, then a safe default.
    private string ResolvePlatformKey(string vendorCode)
    {
        var vendorKey = _configuration[$"Talabat:VendorConfig:{vendorCode}:PlatformKey"];
        if (!string.IsNullOrWhiteSpace(vendorKey))
        {
            return vendorKey;
        }

        var globalKey = _configuration["Talabat:PlatformKey"];
        return string.IsNullOrWhiteSpace(globalKey) ? "TB_KW" : globalKey;
    }
}
