using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;
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
            DueAt = FormatDueAt(dueAt, businessDateTimeZone),
            Meta = BuildMeta(webhook, vendorCode, businessDateTimeZone, businessDateSource)
        };

        return request;
    }

    private List<FoodicsOrderProductRequest> MapProducts(List<TalabatOrderProduct> talabatProducts, int discountType)
    {
        var result = new List<FoodicsOrderProductRequest>();

        foreach (var product in talabatProducts)
        {
            // For Foodics create-order, product_id must come from POS/remote mapping.
            // Talabat "id" is platform-specific and should not be used as Foodics product id.
            var productId = ResolveFoodicsEntityId(product.RemoteCode, MenuMappingEntityType.Product);
            if (string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning(
                    "Skipping Talabat product without remoteCode. ProductName={Name}, TalabatId={TalabatId}",
                    product.Name,
                    product.Id);
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
            // For Foodics create-order, modifier_option_id must come from POS/remote mapping.
            // Talabat "id" is platform-specific and should not be used as Foodics modifier option id.
            var optionId = ResolveFoodicsEntityId(topping.RemoteCode, MenuMappingEntityType.ModifierOption);
            if (string.IsNullOrWhiteSpace(optionId))
            {
                _logger.LogDebug(
                    "Skipping Talabat topping without remoteCode. ToppingName={Name}, TalabatId={TalabatId}",
                    topping.Name,
                    topping.Id);
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

    private string? ResolveFoodicsEntityId(string? talabatRemoteCode, string expectedEntityType)
    {
        if (string.IsNullOrWhiteSpace(talabatRemoteCode))
        {
            return null;
        }

        var normalized = StripTalabatWrapperPrefixes(talabatRemoteCode.Trim());
        var detectedType = MenuMappingStrategy.ExtractEntityType(normalized);
        var extracted = MenuMappingStrategy.ExtractFoodicsId(normalized);

        // Stable remote codes are generated as: P_<foodicsId>, O_<foodicsId>, etc.
        if (!string.IsNullOrWhiteSpace(extracted))
        {
            if (!string.IsNullOrWhiteSpace(detectedType) &&
                !string.Equals(detectedType, expectedEntityType, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "Talabat remoteCode entity type mismatch. RemoteCode={RemoteCode}, Expected={Expected}, Actual={Actual}",
                    talabatRemoteCode,
                    expectedEntityType,
                    detectedType);
            }

            if (!string.Equals(extracted, talabatRemoteCode, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "Resolved Talabat remoteCode to FoodicsId. RemoteCode={RemoteCode}, FoodicsId={FoodicsId}",
                    talabatRemoteCode,
                    extracted);
            }

            return extracted;
        }

        // Legacy mode: remoteCode may already be the raw Foodics ID.
        return normalized;
    }

    private static string StripTalabatWrapperPrefixes(string value)
    {
        var result = value;

        // V2 payloads can wrap stable IDs in helper prefixes.
        if (result.StartsWith("topping-", StringComparison.OrdinalIgnoreCase))
        {
            result = result["topping-".Length..];
        }
        else if (result.StartsWith("product-", StringComparison.OrdinalIgnoreCase))
        {
            result = result["product-".Length..];
        }
        else if (result.StartsWith("option-", StringComparison.OrdinalIgnoreCase))
        {
            result = result["option-".Length..];
        }

        return result;
    }

    private string? FormatDueAt(DateTime? dueAt, string? businessDateTimeZone)
    {
        if (!dueAt.HasValue)
        {
            return null;
        }

        var dueAtValue = dueAt.Value;
        if (string.IsNullOrWhiteSpace(businessDateTimeZone))
        {
            return dueAtValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        try
        {
            var utcDueAt = dueAtValue.Kind switch
            {
                DateTimeKind.Utc => dueAtValue,
                DateTimeKind.Local => dueAtValue.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dueAtValue, DateTimeKind.Utc)
            };

            var timezone = ResolveTimeZoneInfo(businessDateTimeZone);
            var localDueAt = TimeZoneInfo.ConvertTimeFromUtc(utcDueAt, timezone);
            return localDueAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to convert due_at to business timezone. TimeZone={TimeZone}. Falling back to original timestamp.",
                businessDateTimeZone);

            return dueAtValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    private static TimeZoneInfo ResolveTimeZoneInfo(string timezoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Cross-platform alias for Asia/Kuwait when running on Windows.
            if (string.Equals(timezoneId, "Asia/Kuwait", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
            }

            throw;
        }
    }
}
