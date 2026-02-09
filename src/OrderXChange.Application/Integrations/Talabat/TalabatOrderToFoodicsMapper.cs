using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatOrderToFoodicsMapper : ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatOrderToFoodicsMapper> _logger;

    public TalabatOrderToFoodicsMapper(
        IConfiguration configuration,
        ILogger<TalabatOrderToFoodicsMapper> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public FoodicsOrderCreateRequest MapToCreateOrder(
        TalabatOrderWebhook webhook,
        string branchId,
        string vendorCode,
        string? businessDate = null,
        string? businessDateTimeZone = null,
        string? businessDateSource = null)
    {
        if (webhook.Products == null || webhook.Products.Count == 0)
        {
            throw new InvalidOperationException("Talabat order has no products.");
        }

        var orderType = ResolveIntSetting("Foodics:OrderType", ResolveOrderTypeFromExpedition(webhook.ExpeditionType));
        var orderSource = ResolveIntSetting("Foodics:OrderSource", 1);
        var orderStatus = ResolveIntSetting("Foodics:OrderStatus", 1);
        var guests = ResolveIntSetting("Foodics:OrderGuests", 1);
        var discountType = ResolveIntSetting("Foodics:OrderDiscountType", 1);

        var resolvedBusinessDate = businessDate
            ?? (webhook.CreatedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dueAt = webhook.Delivery?.ExpectedDeliveryTime
            ?? webhook.Delivery?.RiderPickupTime
            ?? webhook.ExpiryDate;

        var subtotal = ParseDecimal(webhook.Price?.SubTotal);
        var total = ParseDecimal(webhook.Price?.GrandTotal);
        var discountAmount = ParseDecimal(webhook.Price?.DiscountAmountTotal);

        var products = MapProducts(webhook.Products, discountType);

        if (products.Count == 0)
        {
            throw new InvalidOperationException("Talabat order products missing valid remote codes.");
        }

        var request = new FoodicsOrderCreateRequest
        {
            Type = orderType,
            Source = orderSource,
            Guests = guests,
            Status = orderStatus,
            KitchenNotes = webhook.Comments?.VendorComment,
            CustomerNotes = webhook.Comments?.CustomerComment,
            BusinessDate = resolvedBusinessDate,
            SubtotalPrice = subtotal,
            DiscountAmount = discountAmount,
            TotalPrice = total,
            TaxExclusiveDiscountAmount = null,
            RoundingAmount = 0,
            BranchId = branchId,
            Products = products,
            DueAt = dueAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Meta = BuildMeta(webhook, vendorCode, businessDateTimeZone, businessDateSource)
        };

        return request;
    }

    private List<FoodicsOrderProductRequest> MapProducts(List<TalabatOrderProduct> talabatProducts, int discountType)
    {
        var result = new List<FoodicsOrderProductRequest>();

        foreach (var product in talabatProducts)
        {
            var productId = product.RemoteCode ?? product.Id;
            if (string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning(
                    "Skipping Talabat product without remote code. ProductName={Name}",
                    product.Name);
                continue;
            }

            var quantity = ParseInt(product.Quantity, 1);
            var unitPrice = ParseDecimal(product.PaidPrice) ?? ParseDecimal(product.UnitPrice) ?? 0m;
            var totalPrice = unitPrice * quantity;
            var discountAmount = ParseDecimal(product.DiscountAmount);

            var options = MapOptions(product.SelectedToppings);

            result.Add(new FoodicsOrderProductRequest
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice,
                DiscountAmount = discountAmount,
                DiscountType = discountAmount.HasValue ? discountType : null,
                KitchenNotes = product.Comment,
                Options = options.Count == 0 ? null : options
            });
        }

        return result;
    }

    private List<FoodicsOrderProductOptionRequest> MapOptions(List<TalabatOrderTopping>? toppings)
    {
        var result = new List<FoodicsOrderProductOptionRequest>();
        if (toppings == null || toppings.Count == 0)
        {
            return result;
        }

        foreach (var topping in FlattenToppings(toppings))
        {
            var optionId = topping.RemoteCode ?? topping.Id;
            if (string.IsNullOrWhiteSpace(optionId))
            {
                continue;
            }

            var quantity = topping.Quantity.GetValueOrDefault(1);
            var unitPrice = ParseDecimal(topping.Price) ?? 0m;
            var totalPrice = unitPrice * quantity;

            result.Add(new FoodicsOrderProductOptionRequest
            {
                ModifierOptionId = optionId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice
            });
        }

        return result;
    }

    private static IEnumerable<TalabatOrderTopping> FlattenToppings(IEnumerable<TalabatOrderTopping> toppings)
    {
        foreach (var topping in toppings)
        {
            yield return topping;

            if (topping.Children == null || topping.Children.Count == 0)
            {
                continue;
            }

            foreach (var child in FlattenToppings(topping.Children))
            {
                yield return child;
            }
        }
    }

    private Dictionary<string, object?> BuildMeta(
        TalabatOrderWebhook webhook,
        string vendorCode,
        string? businessDateTimeZone,
        string? businessDateSource)
    {
        var meta = new Dictionary<string, object?>
        {
            ["talabat_token"] = webhook.Token,
            ["talabat_code"] = webhook.Code,
            ["talabat_short_code"] = webhook.ShortCode,
            ["vendor_code"] = vendorCode,
            ["expedition_type"] = webhook.ExpeditionType,
            ["platform_key"] = webhook.LocalInfo?.PlatformKey,
            ["platform_restaurant_id"] = webhook.PlatformRestaurant?.Id,
            ["test_order"] = webhook.Test,
            ["business_date_timezone"] = businessDateTimeZone,
            ["business_date_source"] = businessDateSource
        };

        if (webhook.Customer != null)
        {
            meta["customer_email"] = webhook.Customer.Email;
            meta["customer_name"] = string.Join(" ", new[] { webhook.Customer.FirstName, webhook.Customer.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
            meta["customer_phone"] = webhook.Customer.MobilePhone;
            meta["customer_code"] = webhook.Customer.Code;
        }

        if (webhook.Delivery?.Address != null)
        {
            meta["delivery_city"] = webhook.Delivery.Address.City;
            meta["delivery_street"] = webhook.Delivery.Address.Street;
            meta["delivery_number"] = webhook.Delivery.Address.Number;
            meta["delivery_postcode"] = webhook.Delivery.Address.Postcode;
        }

        return meta;
    }

    private int ResolveOrderTypeFromExpedition(string? expeditionType)
    {
        if (string.IsNullOrWhiteSpace(expeditionType))
        {
            return 1;
        }

        return expeditionType.ToLowerInvariant() switch
        {
            "delivery" => ResolveIntSetting("Foodics:OrderTypeDelivery", 1),
            "pickup" => ResolveIntSetting("Foodics:OrderTypePickup", 1),
            _ => ResolveIntSetting("Foodics:OrderType", 1)
        };
    }

    private int ResolveIntSetting(string key, int fallback)
    {
        var value = _configuration[key];
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static int ParseInt(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace(",", ".", StringComparison.Ordinal);

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
