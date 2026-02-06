using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsSingleEnvelope<TItem>
{
    [JsonPropertyName("data")]
    public TItem? Data { get; set; }
}

public class FoodicsOrderCreateRequest
{
    [JsonPropertyName("type")]
    public int? Type { get; set; }

    [JsonPropertyName("source")]
    public int? Source { get; set; }

    [JsonPropertyName("guests")]
    public int? Guests { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("kitchen_notes")]
    public string? KitchenNotes { get; set; }

    [JsonPropertyName("customer_notes")]
    public string? CustomerNotes { get; set; }

    [JsonPropertyName("business_date")]
    public string? BusinessDate { get; set; }

    [JsonPropertyName("subtotal_price")]
    public decimal? SubtotalPrice { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal? DiscountAmount { get; set; }

    [JsonPropertyName("rounding_amount")]
    public decimal? RoundingAmount { get; set; }

    [JsonPropertyName("total_price")]
    public decimal? TotalPrice { get; set; }

    [JsonPropertyName("tax_exclusive_discount_amount")]
    public decimal? TaxExclusiveDiscountAmount { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, object?>? Meta { get; set; }

    [JsonPropertyName("branch_id")]
    public string BranchId { get; set; } = string.Empty;

    [JsonPropertyName("table_id")]
    public string? TableId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("creator_id")]
    public string? CreatorId { get; set; }

    [JsonPropertyName("closer_id")]
    public string? CloserId { get; set; }

    [JsonPropertyName("original_order_id")]
    public string? OriginalOrderId { get; set; }

    [JsonPropertyName("driver_id")]
    public string? DriverId { get; set; }

    [JsonPropertyName("customer_id")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("customer_address_id")]
    public string? CustomerAddressId { get; set; }

    [JsonPropertyName("discount_id")]
    public string? DiscountId { get; set; }

    [JsonPropertyName("coupon_id")]
    public string? CouponId { get; set; }

    [JsonPropertyName("payments")]
    public List<FoodicsOrderPaymentRequest>? Payments { get; set; }

    [JsonPropertyName("charges")]
    public List<FoodicsOrderChargeRequest>? Charges { get; set; }

    [JsonPropertyName("gift_card")]
    public FoodicsOrderGiftCardRequest? GiftCard { get; set; }

    [JsonPropertyName("products")]
    public List<FoodicsOrderProductRequest> Products { get; set; } = new();

    [JsonPropertyName("combos")]
    public List<FoodicsOrderComboRequest>? Combos { get; set; }

    [JsonPropertyName("tags")]
    public List<FoodicsOrderTagRequest>? Tags { get; set; }

    [JsonPropertyName("promotion")]
    public string? PromotionId { get; set; }

    [JsonPropertyName("due_at")]
    public string? DueAt { get; set; }
}

public class FoodicsOrderPaymentRequest
{
    [JsonPropertyName("business_date")]
    public string? BusinessDate { get; set; }

    [JsonPropertyName("payment_method_id")]
    public string? PaymentMethodId { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("tips")]
    public decimal? Tips { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, object?>? Meta { get; set; }
}

public class FoodicsOrderChargeRequest
{
    [JsonPropertyName("charge_id")]
    public string? ChargeId { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("tax_exclusive_amount")]
    public decimal? TaxExclusiveAmount { get; set; }
}

public class FoodicsOrderGiftCardRequest
{
    [JsonPropertyName("gift_card_product_id")]
    public string? GiftCardProductId { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class FoodicsOrderProductRequest
{
    [JsonPropertyName("product_id")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("discount_id")]
    public string? DiscountId { get; set; }

    [JsonPropertyName("promotion_id")]
    public string? PromotionId { get; set; }

    [JsonPropertyName("options")]
    public List<FoodicsOrderProductOptionRequest>? Options { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal? DiscountAmount { get; set; }

    [JsonPropertyName("discount_type")]
    public int? DiscountType { get; set; }

    [JsonPropertyName("total_price")]
    public decimal? TotalPrice { get; set; }

    [JsonPropertyName("tax_exclusive_discount_amount")]
    public decimal? TaxExclusiveDiscountAmount { get; set; }

    [JsonPropertyName("tax_exclusive_unit_price")]
    public decimal? TaxExclusiveUnitPrice { get; set; }

    [JsonPropertyName("tax_exclusive_total_price")]
    public decimal? TaxExclusiveTotalPrice { get; set; }

    [JsonPropertyName("kitchen_notes")]
    public string? KitchenNotes { get; set; }
}

public class FoodicsOrderProductOptionRequest
{
    [JsonPropertyName("modifier_option_id")]
    public string ModifierOptionId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("partition")]
    public int? Partition { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("total_price")]
    public decimal? TotalPrice { get; set; }

    [JsonPropertyName("tax_exclusive_unit_price")]
    public decimal? TaxExclusiveUnitPrice { get; set; }

    [JsonPropertyName("tax_exclusive_total_price")]
    public decimal? TaxExclusiveTotalPrice { get; set; }
}

public class FoodicsOrderComboRequest
{
    [JsonPropertyName("combo_size_id")]
    public string? ComboSizeId { get; set; }

    [JsonPropertyName("discount_id")]
    public string? DiscountId { get; set; }

    [JsonPropertyName("discount_type")]
    public int? DiscountType { get; set; }

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("returned_quantity")]
    public int? ReturnedQuantity { get; set; }

    [JsonPropertyName("products")]
    public List<FoodicsOrderComboProductRequest>? Products { get; set; }
}

public class FoodicsOrderComboProductRequest
{
    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("combo_option_id")]
    public string? ComboOptionId { get; set; }

    [JsonPropertyName("combo_size_id")]
    public string? ComboSizeId { get; set; }

    [JsonPropertyName("total_price")]
    public decimal? TotalPrice { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }
}

public class FoodicsOrderTagRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public class FoodicsOrderResponseDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}
