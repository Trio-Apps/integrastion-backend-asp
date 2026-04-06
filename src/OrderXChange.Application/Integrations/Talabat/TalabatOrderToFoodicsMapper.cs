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
    private readonly ILogger<TalabatOrderToFoodicsMapper> _logger;

    public TalabatOrderToFoodicsMapper(
        IConfiguration configuration,
        ILogger<TalabatOrderToFoodicsMapper> logger)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private readonly IConfiguration _configuration;

    public FoodicsOrderCreateRequest MapToCreateOrder(
        TalabatOrderWebhook webhook,
        string branchId,
        string vendorCode,
        string? businessDate = null,
        string? businessDateTimeZone = null,
        string? businessDateSource = null,
        string? activePaymentMethodId = null,
        string? deliveryChargeId = null)
    {
        if (webhook.Products == null || webhook.Products.Count == 0)
        {
            throw new InvalidOperationException("Talabat order has no products.");
        }

        var orderType = ResolveOrderTypeFromExpedition(webhook.ExpeditionType);
        var orderSource = ResolveIntSetting("Foodics:OrderSource", 1);
        var orderStatus = ResolveIntSetting("Foodics:OrderStatus", 1);
        var guests = ResolveIntSetting("Foodics:OrderGuests", 1);
        var discountType = ResolveIntSetting("Foodics:OrderDiscountType", 1);

        var createdAt = webhook.CreatedAt ?? DateTime.UtcNow;
        var resolvedBusinessDate = businessDate
            ?? createdAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dueAt = webhook.Delivery?.ExpectedDeliveryTime
            ?? webhook.Delivery?.RiderPickupTime
            ?? webhook.ExpiryDate;

        var subtotal = ParseDecimal(webhook.Price?.SubTotal);
        var totalNet = ParseDecimal(webhook.Price?.TotalNet);
        var grandTotal = ParseDecimal(webhook.Price?.GrandTotal);
        var deliveryFeeAmount = ResolveDeliveryFeeAmount(webhook.Price);
        var charges = BuildCharges(deliveryFeeAmount, deliveryChargeId);
        var chargesTotal = SumChargeAmounts(charges);
        var total = totalNet ?? subtotal ?? grandTotal;
        var foodicsTotalPrice = ResolveFoodicsTotalPrice(subtotal, total, chargesTotal);
        var reportedOrderDiscountAmount = ResolveDiscountAmount(
            ParseDecimal(webhook.Price?.DiscountAmountTotal),
            webhook.Discounts);
        var reportedItemDiscountAmount = CalculateTalabatItemDiscountAmount(webhook.Products);
        var paymentMethodId = ResolvePaymentMethodId(webhook, activePaymentMethodId);
        var talabatOrderReferenceNote = BuildTalabatOrderReferenceNote(webhook);

        var products = MapProducts(webhook.Products, discountType);
        var paymentAmount = ResolvePaymentAmount(webhook, products, null, total, subtotal, grandTotal, chargesTotal);

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
            KitchenNotes = MergeNotes(talabatOrderReferenceNote, webhook.Comments?.VendorComment),
            CustomerNotes = MergeNotes(talabatOrderReferenceNote, webhook.Comments?.CustomerComment),
            BusinessDate = resolvedBusinessDate,
            CreatedAt = FormatCreatedAt(createdAt, businessDateTimeZone),
            SubtotalPrice = subtotal,
            DiscountAmount = null,
            TotalPrice = foodicsTotalPrice,
            TaxExclusiveDiscountAmount = null,
            RoundingAmount = 0,
            BranchId = branchId,
            Products = products,
            Payments = BuildPayments(resolvedBusinessDate, paymentMethodId, paymentAmount),
            Charges = charges,
            DueAt = FormatDueAt(dueAt, businessDateTimeZone),
            Meta = BuildMeta(webhook, vendorCode, businessDateTimeZone, businessDateSource, reportedOrderDiscountAmount, reportedItemDiscountAmount, deliveryFeeAmount)
        };

        return request;
    }

    private static string? BuildTalabatOrderReferenceNote(TalabatOrderWebhook webhook)
    {
        var orderCode = webhook.Code?.Trim();
        var shortCode = webhook.ShortCode?.Trim();

        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            return string.IsNullOrWhiteSpace(shortCode)
                ? $"Talabat Order ID: {orderCode}"
                : $"Talabat Order ID: {orderCode} | Talabat Short Code: {shortCode}";
        }

        return string.IsNullOrWhiteSpace(shortCode)
            ? null
            : $"Talabat Short Code: {shortCode}";
    }

    private static string? MergeNotes(string? primaryNote, string? existingNote)
    {
        var parts = new[] { primaryNote?.Trim(), existingNote?.Trim() }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return parts.Count == 0
            ? null
            : string.Join(Environment.NewLine, parts);
    }

    private List<FoodicsOrderProductRequest> MapProducts(List<TalabatOrderProduct> talabatProducts, int discountType)
    {
        var result = new List<FoodicsOrderProductRequest>();
        var lineTemplates = BuildOptionTemplates(talabatProducts, useLineKey: true);
        var productTemplates = BuildOptionTemplates(talabatProducts, useLineKey: false);
        var lineCounts = talabatProducts
            .GroupBy(GetProductLineKey)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

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
            var paidPrice = ParseDecimal(product.PaidPrice);
            var baseUnitPrice = ParseDecimal(product.UnitPrice) ?? 0m;
            var totalPrice = paidPrice ?? (baseUnitPrice * quantity);
            var unitPrice = quantity > 0
                ? Math.Round(totalPrice / quantity, 3, MidpointRounding.AwayFromZero)
                : baseUnitPrice;
            var discountAmount = (decimal?)null;

            var options = MapOptions(product.SelectedToppings, product.SelectedChoices);
            var lineKey = GetProductLineKey(product);
            var productKey = GetProductRemoteCodeKey(product);
            var hasSplitSiblingLines = lineCounts.TryGetValue(lineKey, out var siblingCount) && siblingCount > 1;

            // Some Talabat orders split the same product into multiple lines.
            // Do not merge/reuse sibling options in that case because each line can represent a different selection set.
            if (options.Count == 0 &&
                !hasSplitSiblingLines &&
                lineTemplates.TryGetValue(lineKey, out var lineTemplate) &&
                lineTemplate.Count > 0)
            {
                options = CloneOptions(lineTemplate);
                _logger.LogWarning(
                    "Order line missing selected toppings. Reusing options from same line template. ProductName={ProductName}, ProductKey={ProductKey}, OptionCount={OptionCount}",
                    product.Name,
                    lineKey,
                    options.Count);
            }
            else if (options.Count == 0 &&
                     !hasSplitSiblingLines &&
                     productTemplates.TryGetValue(productKey, out var productTemplate) &&
                     productTemplate.Count > 0)
            {
                options = CloneOptions(productTemplate);
                _logger.LogWarning(
                    "Order line missing selected toppings. Reusing options from same product. ProductName={ProductName}, ProductRemoteCode={ProductRemoteCode}, OptionCount={OptionCount}",
                    product.Name,
                    product.RemoteCode,
                    options.Count);
            }

            result.Add(new FoodicsOrderProductRequest
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice,
                DiscountAmount = null,
                DiscountType = discountAmount.HasValue ? discountType : null,
                KitchenNotes = product.Comment,
                Options = options.Count == 0 ? null : options
            });
        }

        return result;
    }

    private List<FoodicsOrderProductOptionRequest> MapOptions(List<TalabatOrderTopping>? toppings, List<TalabatOrderChoice>? choices)
    {
        var result = new List<FoodicsOrderProductOptionRequest>();
        var optionsById = new Dictionary<string, FoodicsOrderProductOptionRequest>(StringComparer.OrdinalIgnoreCase);

        if (toppings != null && toppings.Count > 0)
        {
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

                optionsById[optionId] = new FoodicsOrderProductOptionRequest
                {
                    ModifierOptionId = optionId,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = totalPrice
                };
            }
        }

        // Talabat may send fallback choice values instead of selectedToppings for some flows.
        if (choices != null && choices.Count > 0)
        {
            foreach (var choice in choices)
            {
                var candidateRemoteCode = TryExtractOptionRemoteCode(choice.Value) ?? TryExtractOptionRemoteCode(choice.Name);
                if (string.IsNullOrWhiteSpace(candidateRemoteCode))
                {
                    continue;
                }

                var optionId = ResolveFoodicsEntityId(candidateRemoteCode, MenuMappingEntityType.ModifierOption);
                if (string.IsNullOrWhiteSpace(optionId))
                {
                    continue;
                }

                if (!optionsById.ContainsKey(optionId))
                {
                    optionsById[optionId] = new FoodicsOrderProductOptionRequest
                    {
                        ModifierOptionId = optionId,
                        Quantity = 1,
                        UnitPrice = 0m,
                        TotalPrice = 0m
                    };

                    _logger.LogInformation(
                        "Mapped order option from selectedChoices fallback. ChoiceName={ChoiceName}, ChoiceValue={ChoiceValue}, ModifierOptionId={ModifierOptionId}",
                        choice.Name,
                        choice.Value,
                        optionId);
                }
            }
        }

        result.AddRange(optionsById.Values);
        return result;
    }

    private List<FoodicsOrderPaymentRequest>? BuildPayments(
        string businessDate,
        string? paymentMethodId,
        decimal? paymentAmount)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodId))
        {
            return null;
        }

        if (!paymentAmount.HasValue || paymentAmount.Value <= 0m)
        {
            return null;
        }

        return new List<FoodicsOrderPaymentRequest>
        {
            new()
            {
                BusinessDate = businessDate,
                PaymentMethodId = paymentMethodId,
                Amount = paymentAmount,
                Tendered = paymentAmount,
                Tips = 0m
            }
        };
    }

    private List<FoodicsOrderChargeRequest>? BuildCharges(decimal? deliveryFeeAmount, string? deliveryChargeId)
    {
        if (!deliveryFeeAmount.HasValue || deliveryFeeAmount.Value <= 0m)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(deliveryChargeId))
        {
            _logger.LogWarning(
                "Talabat order contains delivery fee {DeliveryFeeAmount} but no Foodics delivery charge is available. Delivery fee will not be sent as a separate charge.",
                deliveryFeeAmount.Value);
            return null;
        }

        return new List<FoodicsOrderChargeRequest>
        {
            new()
            {
                ChargeId = deliveryChargeId.Trim(),
                Amount = deliveryFeeAmount.Value
            }
        };
    }

    private static decimal? SumChargeAmounts(IReadOnlyCollection<FoodicsOrderChargeRequest>? charges)
    {
        if (charges == null || charges.Count == 0)
        {
            return null;
        }

        decimal total = 0m;
        var hasAnyAmount = false;

        foreach (var charge in charges)
        {
            if (!charge.Amount.HasValue)
            {
                continue;
            }

            total += charge.Amount.Value;
            hasAnyAmount = true;
        }

        return hasAnyAmount
            ? total
            : null;
    }

    private string? ResolvePaymentMethodId(TalabatOrderWebhook webhook, string? activePaymentMethodId)
    {
        var paymentMethodId = string.IsNullOrWhiteSpace(activePaymentMethodId)
            ? null
            : activePaymentMethodId.Trim();
        if (!string.IsNullOrWhiteSpace(paymentMethodId))
        {
            return paymentMethodId;
        }

        _logger.LogDebug(
            "No active Foodics payment method selected in dashboard for Talabat order. OrderCode={OrderCode}, PaymentType={PaymentType}, PaymentRemoteCode={PaymentRemoteCode}",
            webhook.Code,
            webhook.Payment?.Type,
            webhook.Payment?.RemoteCode);

        return null;
    }

    private static decimal? ResolvePaymentAmount(
        TalabatOrderWebhook webhook,
        IReadOnlyCollection<FoodicsOrderProductRequest> products,
        decimal? orderDiscountAmount,
        decimal? total,
        decimal? subtotal,
        decimal? grandTotal,
        decimal? chargesTotal)
    {
        var explicitTotal = ResolveExplicitPaymentAmount(webhook, grandTotal, total, subtotal);
        var mappedNetTotal = CalculateMappedNetTotal(products, orderDiscountAmount);
        if (mappedNetTotal.HasValue && chargesTotal.HasValue && chargesTotal.Value > 0m)
        {
            mappedNetTotal += chargesTotal.Value;
        }

        if (mappedNetTotal.HasValue && explicitTotal.HasValue)
        {
            // Talabat totals are occasionally inconsistent with line totals for multi-quantity items.
            // When that happens, trust the mapped lines plus charges to keep Foodics payments balanced.
            return Math.Abs(mappedNetTotal.Value - explicitTotal.Value) <= 0.01m
                ? explicitTotal.Value
                : mappedNetTotal.Value;
        }

        if (mappedNetTotal.HasValue)
        {
            return mappedNetTotal;
        }

        return explicitTotal;
    }

    private static decimal? ResolveExplicitPaymentAmount(TalabatOrderWebhook webhook, decimal? grandTotal, decimal? total, decimal? subtotal)
    {
        if (grandTotal.HasValue && grandTotal.Value > 0m)
        {
            return grandTotal;
        }

        if (total.HasValue && total.Value > 0m)
        {
            return total;
        }

        if (subtotal.HasValue && subtotal.Value > 0m)
        {
            return subtotal;
        }

        var collectFromCustomer = ParseDecimal(webhook.Price?.CollectFromCustomer);
        if (collectFromCustomer.HasValue && collectFromCustomer.Value > 0m)
        {
            return collectFromCustomer;
        }

        return null;
    }

    private static decimal? ResolveExplicitPaymentAmount(TalabatOrderWebhook webhook, decimal? total, decimal? subtotal)
    {
        if (total.HasValue && total.Value > 0m)
        {
            return total;
        }

        if (subtotal.HasValue && subtotal.Value > 0m)
        {
            return subtotal;
        }

        var collectFromCustomer = ParseDecimal(webhook.Price?.CollectFromCustomer);
        if (collectFromCustomer.HasValue && collectFromCustomer.Value > 0m)
        {
            return collectFromCustomer;
        }

        return null;
    }

    private static decimal? ResolveDeliveryFeeAmount(TalabatOrderPrice? price)
    {
        if (price == null)
        {
            return null;
        }

        var directDeliveryFee = ParseDecimal(price.DeliveryFee);
        if (directDeliveryFee.HasValue && directDeliveryFee.Value > 0m)
        {
            return directDeliveryFee.Value;
        }

        if (price.DeliveryFees == null || price.DeliveryFees.Count == 0)
        {
            return null;
        }

        decimal total = 0m;
        var hasAnyValue = false;

        foreach (var fee in price.DeliveryFees)
        {
            if (!fee.Value.HasValue)
            {
                continue;
            }

            total += fee.Value.Value;
            hasAnyValue = true;
        }

        return hasAnyValue && total > 0m
            ? total
            : null;
    }

    private static decimal? ResolveFoodicsTotalPrice(decimal? subtotal, decimal? total, decimal? chargesTotal)
    {
        if (subtotal.HasValue && subtotal.Value > 0m)
        {
            return subtotal.Value;
        }

        if (total.HasValue && total.Value > 0m)
        {
            if (chargesTotal.HasValue && chargesTotal.Value > 0m)
            {
                var netTotal = total.Value - chargesTotal.Value;
                return netTotal > 0m
                    ? netTotal
                    : 0m;
            }

            return total.Value;
        }

        return null;
    }

    private static decimal? CalculateMappedNetTotal(
        IReadOnlyCollection<FoodicsOrderProductRequest> products,
        decimal? orderDiscountAmount)
    {
        if (products == null || products.Count == 0)
        {
            return null;
        }

        decimal grossTotal = 0m;
        decimal lineDiscountTotal = 0m;
        var hasAnyAmount = false;

        foreach (var product in products)
        {
            if (product.TotalPrice.HasValue)
            {
                grossTotal += product.TotalPrice.Value;
                hasAnyAmount = true;
            }

            if (product.Options != null)
            {
                foreach (var option in product.Options)
                {
                    if (!option.TotalPrice.HasValue)
                    {
                        continue;
                    }

                    grossTotal += option.TotalPrice.Value;
                    hasAnyAmount = true;
                }
            }

            if (product.DiscountAmount.HasValue)
            {
                lineDiscountTotal += product.DiscountAmount.Value;
            }
        }

        if (!hasAnyAmount)
        {
            return null;
        }

        var netTotal = grossTotal - lineDiscountTotal - (orderDiscountAmount ?? 0m);
        return netTotal > 0m ? netTotal : 0m;
    }

    private static decimal? ResolveDiscountAmount(decimal? directAmount, List<TalabatOrderDiscount>? discounts)
    {
        if (directAmount.HasValue && directAmount.Value > 0m)
        {
            return directAmount;
        }

        var aggregatedDiscount = SumDiscountAmounts(discounts);
        if (aggregatedDiscount.HasValue && aggregatedDiscount.Value > 0m)
        {
            return aggregatedDiscount;
        }

        return null;
    }

    private static decimal? SumDiscountAmounts(List<TalabatOrderDiscount>? discounts)
    {
        if (discounts == null || discounts.Count == 0)
        {
            return null;
        }

        decimal total = 0m;
        var hasAnyValue = false;

        foreach (var discount in discounts)
        {
            var amount = ParseDecimal(discount.Amount);
            if (!amount.HasValue)
            {
                continue;
            }

            total += Math.Abs(amount.Value);
            hasAnyValue = true;
        }

        return hasAnyValue && total > 0m
            ? total
            : null;
    }

    private static decimal? CalculateTalabatItemDiscountAmount(IEnumerable<TalabatOrderProduct>? products)
    {
        if (products == null)
        {
            return null;
        }

        decimal total = 0m;
        var hasAnyValue = false;

        foreach (var product in products)
        {
            var lineDiscount = ResolveDiscountAmount(
                ParseDecimal(product.DiscountAmount),
                product.Discounts);

            if (!lineDiscount.HasValue || lineDiscount.Value <= 0m)
            {
                continue;
            }

            total += lineDiscount.Value;
            hasAnyValue = true;
        }

        return hasAnyValue && total > 0m
            ? total
            : null;
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
        string? businessDateSource,
        decimal? reportedOrderDiscountAmount,
        decimal? reportedItemDiscountAmount,
        decimal? deliveryFeeAmount)
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
            ["business_date_source"] = businessDateSource,
            ["external_order_source"] = "foodics_kiosk"
        };

        if (deliveryFeeAmount.HasValue && deliveryFeeAmount.Value > 0m)
        {
            meta["talabat_delivery_fee_amount"] = deliveryFeeAmount;
        }

        if (reportedOrderDiscountAmount.HasValue && reportedOrderDiscountAmount.Value > 0m)
        {
            meta["talabat_order_discount_amount"] = reportedOrderDiscountAmount;
        }

        if (reportedItemDiscountAmount.HasValue && reportedItemDiscountAmount.Value > 0m)
        {
            meta["talabat_item_discount_amount"] = reportedItemDiscountAmount;
        }

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
            "delivery" => ResolveIntSetting("Foodics:OrderTypeDelivery", 3),
            "pickup" => ResolveIntSetting("Foodics:OrderTypePickup", 2),
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

    private static string GetProductLineKey(TalabatOrderProduct product)
    {
        var productId = string.IsNullOrWhiteSpace(product.Id) ? "<no-id>" : product.Id.Trim();
        var remoteCode = string.IsNullOrWhiteSpace(product.RemoteCode) ? "<no-remote>" : product.RemoteCode.Trim();
        return $"{productId}|{remoteCode}";
    }

    private static string GetProductRemoteCodeKey(TalabatOrderProduct product)
    {
        return string.IsNullOrWhiteSpace(product.RemoteCode)
            ? "<no-remote>"
            : product.RemoteCode.Trim();
    }

    private Dictionary<string, List<FoodicsOrderProductOptionRequest>> BuildOptionTemplates(
        List<TalabatOrderProduct> products,
        bool useLineKey)
    {
        var grouped = useLineKey
            ? products.GroupBy(GetProductLineKey)
            : products.GroupBy(GetProductRemoteCodeKey);

        var result = new Dictionary<string, List<FoodicsOrderProductOptionRequest>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in grouped)
        {
            var merged = new List<FoodicsOrderProductOptionRequest>();
            foreach (var product in group)
            {
                var mapped = MapOptions(product.SelectedToppings, product.SelectedChoices);
                merged = MergeOptions(merged, mapped);
            }

            if (merged.Count > 0)
            {
                result[group.Key] = merged;
            }
        }

        return result;
    }

    private static List<FoodicsOrderProductOptionRequest> MergeOptions(
        IEnumerable<FoodicsOrderProductOptionRequest> existing,
        IEnumerable<FoodicsOrderProductOptionRequest> incoming)
    {
        var merged = new Dictionary<string, FoodicsOrderProductOptionRequest>(StringComparer.OrdinalIgnoreCase);

        static FoodicsOrderProductOptionRequest Clone(FoodicsOrderProductOptionRequest source)
        {
            return new FoodicsOrderProductOptionRequest
            {
                ModifierOptionId = source.ModifierOptionId,
                Quantity = source.Quantity,
                UnitPrice = source.UnitPrice,
                TotalPrice = source.TotalPrice
            };
        }

        foreach (var option in existing.Concat(incoming))
        {
            if (string.IsNullOrWhiteSpace(option.ModifierOptionId))
            {
                continue;
            }

            if (merged.TryGetValue(option.ModifierOptionId, out var current))
            {
                current.Quantity = Math.Max(current.Quantity, option.Quantity);
                if (current.UnitPrice == 0m && option.UnitPrice > 0m)
                {
                    current.UnitPrice = option.UnitPrice;
                }

                current.TotalPrice = current.UnitPrice * current.Quantity;
                continue;
            }

            merged[option.ModifierOptionId] = Clone(option);
        }

        return merged.Values.ToList();
    }

    private static List<FoodicsOrderProductOptionRequest> CloneOptions(IEnumerable<FoodicsOrderProductOptionRequest> source)
    {
        return source.Select(x => new FoodicsOrderProductOptionRequest
        {
            ModifierOptionId = x.ModifierOptionId,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            TotalPrice = x.TotalPrice
        }).ToList();
    }

    private static string? TryExtractOptionRemoteCode(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var value = rawValue.Trim();
        var prefixes = new[] { "topping-O_", "option-O_", "O_" };
        foreach (var prefix in prefixes)
        {
            var index = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var token = value[index..];
            var end = token.IndexOfAny(new[] { ' ', ',', ';', '|', ')', ']', '}', '"', '\'' });
            return end >= 0 ? token[..end] : token;
        }

        return null;
    }
    private string FormatCreatedAt(DateTime createdAt, string? businessDateTimeZone)
    {
        if (string.IsNullOrWhiteSpace(businessDateTimeZone))
        {
            return createdAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        try
        {
            var utcCreatedAt = createdAt.Kind switch
            {
                DateTimeKind.Utc => createdAt,
                DateTimeKind.Local => createdAt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
            };

            var timezone = ResolveTimeZoneInfo(businessDateTimeZone);
            var localCreatedAt = TimeZoneInfo.ConvertTimeFromUtc(utcCreatedAt, timezone);
            return localCreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to convert created_at to business timezone. TimeZone={TimeZone}. Falling back to original timestamp.",
                businessDateTimeZone);

            return createdAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
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




