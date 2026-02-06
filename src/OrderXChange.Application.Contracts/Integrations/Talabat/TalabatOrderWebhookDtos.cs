using System;
using System.Collections.Generic;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

public class TalabatOrderWebhook
{
	public string? Token { get; set; }
	public string? Code { get; set; }
	public TalabatOrderComments? Comments { get; set; }
	public DateTime? CreatedAt { get; set; }
	public TalabatOrderCustomer? Customer { get; set; }
	public TalabatOrderDelivery? Delivery { get; set; }
	public List<TalabatOrderDiscount>? Discounts { get; set; }
	public string? ExpeditionType { get; set; }
	public DateTime? ExpiryDate { get; set; }
	public Dictionary<string, string>? ExtraParameters { get; set; }
	public TalabatOrderLocalInfo? LocalInfo { get; set; }
	public TalabatOrderPayment? Payment { get; set; }
	public bool? Test { get; set; }
	public string? ShortCode { get; set; }
	public bool? PreOrder { get; set; }
	public TalabatOrderPickup? Pickup { get; set; }
	public TalabatPlatformRestaurant? PlatformRestaurant { get; set; }
	public TalabatOrderPrice? Price { get; set; }
	public List<TalabatOrderProduct>? Products { get; set; }
	public bool? CorporateOrder { get; set; }
	public string? CorporateTaxId { get; set; }
	public Dictionary<string, object>? IntegrationInfo { get; set; }
	public bool? MobileOrder { get; set; }
	public bool? WebOrder { get; set; }
	public List<TalabatOrderVoucher>? Vouchers { get; set; }
	public TalabatOrderCallbackUrls? CallbackUrls { get; set; }
}

public class TalabatOrderComments
{
	public string? CustomerComment { get; set; }
	public string? VendorComment { get; set; }
}

public class TalabatOrderCustomer
{
	public string? Email { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? MobilePhone { get; set; }
	public string? Code { get; set; }
	public string? Id { get; set; }
	public string? MobilePhoneCountryCode { get; set; }
	public List<string>? Flags { get; set; }
}

public class TalabatOrderDelivery
{
	public TalabatOrderAddress? Address { get; set; }
	public DateTime? ExpectedDeliveryTime { get; set; }
	public bool? ExpressDelivery { get; set; }
	public DateTime? RiderPickupTime { get; set; }
}

public class TalabatOrderAddress
{
	public string? Postcode { get; set; }
	public string? City { get; set; }
	public string? Street { get; set; }
	public string? Number { get; set; }
}

public class TalabatOrderDiscount
{
	public string? Name { get; set; }
	public string? Amount { get; set; }
	public string? Type { get; set; }
}

public class TalabatOrderLocalInfo
{
	public string? CountryCode { get; set; }
	public string? CurrencySymbol { get; set; }
	public string? Platform { get; set; }
	public string? PlatformKey { get; set; }
	public string? CurrencySymbolPosition { get; set; }
	public string? CurrencySymbolSpaces { get; set; }
	public string? DecimalDigits { get; set; }
	public string? DecimalSeparator { get; set; }
	public string? Email { get; set; }
	public string? Phone { get; set; }
	public string? ThousandsSeparator { get; set; }
	public string? Website { get; set; }
}

public class TalabatOrderPayment
{
	public string? Status { get; set; }
	public string? Type { get; set; }
	public string? RemoteCode { get; set; }
	public string? RequiredMoneyChange { get; set; }
	public string? VatId { get; set; }
	public string? VatName { get; set; }
}

public class TalabatOrderPickup
{
	public string? Type { get; set; }
	public string? Notes { get; set; }
}

public class TalabatPlatformRestaurant
{
	public string? Id { get; set; }
}

public class TalabatOrderPrice
{
	public List<TalabatOrderDeliveryFee>? DeliveryFees { get; set; }
	public string? GrandTotal { get; set; }
	public string? MinimumDeliveryValue { get; set; }
	public string? PayRestaurant { get; set; }
	public string? RiderTip { get; set; }
	public string? SubTotal { get; set; }
	public string? TotalNet { get; set; }
	public string? VatTotal { get; set; }
	public string? Comission { get; set; }
	public string? ContainerCharge { get; set; }
	public string? DeliveryFee { get; set; }
	public string? CollectFromCustomer { get; set; }
	public string? DiscountAmountTotal { get; set; }
	public string? DeliveryFeeDiscount { get; set; }
	public string? ServiceFeePercent { get; set; }
	public string? ServiceFeeTotal { get; set; }
	public decimal? ServiceTax { get; set; }
	public decimal? ServiceTaxValue { get; set; }
	public string? DifferenceToMinimumDeliveryValue { get; set; }
	public bool? VatVisible { get; set; }
	public string? VatPercent { get; set; }
}

public class TalabatOrderDeliveryFee
{
	public string? Name { get; set; }
	public decimal? Value { get; set; }
}

public class TalabatOrderProduct
{
	public string? CategoryName { get; set; }
	public string? Name { get; set; }
	public string? PaidPrice { get; set; }
	public string? Quantity { get; set; }
	public string? RemoteCode { get; set; }
	public List<TalabatOrderTopping>? SelectedToppings { get; set; }
	public string? UnitPrice { get; set; }
	public string? Comment { get; set; }
	public string? Description { get; set; }
	public string? DiscountAmount { get; set; }
	public bool? HalfHalf { get; set; }
	public string? Id { get; set; }
	public List<TalabatOrderChoice>? SelectedChoices { get; set; }
	public TalabatOrderVariation? Variation { get; set; }
	public string? VatPercentage { get; set; }
}

public class TalabatOrderTopping
{
	public List<TalabatOrderTopping>? Children { get; set; }
	public string? Name { get; set; }
	public string? Price { get; set; }
	public int? Quantity { get; set; }
	public string? Id { get; set; }
	public string? RemoteCode { get; set; }
	public string? Type { get; set; }
}

public class TalabatOrderChoice
{
	public string? Name { get; set; }
	public string? Value { get; set; }
}

public class TalabatOrderVariation
{
	public string? Name { get; set; }
}

public class TalabatOrderVoucher
{
	public string? Code { get; set; }
	public string? Amount { get; set; }
}

public class TalabatOrderCallbackUrls
{
	public string? OrderAcceptedUrl { get; set; }
	public string? OrderRejectedUrl { get; set; }
	public string? OrderPickedUpUrl { get; set; }
	public string? OrderPreparedUrl { get; set; }
}
