using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

public class GetTalabatOrderLogsInput : PagedAndSortedResultRequestDto
{
    public string? SearchTerm { get; set; }
    public string? VendorCode { get; set; }
    public string? BranchId { get; set; }

    /// <summary>Delivery platform to filter by, e.g. "Talabat".</summary>
    public string? Aggregator { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Status { get; set; }
    public bool? IsTestOrder { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class TalabatOrderLogDto
{
    public Guid Id { get; set; }
    public Guid FoodicsAccountId { get; set; }
    public string? VendorCode { get; set; }

    /// <summary>Delivery platform this order came from, e.g. "Talabat".</summary>
    public string? Aggregator { get; set; }
    public string? PlatformRestaurantId { get; set; }
    public string? OrderToken { get; set; }
    public string? OrderCode { get; set; }
    public string? ShortCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsTestOrder { get; set; }
    public int ProductsCount { get; set; }
    public int CategoriesCount { get; set; }
    public DateTime? OrderCreatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreationTime { get; set; }
    // Listing fields parsed from webhook at receipt (§2 Orders Console)
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ExpeditionType { get; set; }
    public string? Channel { get; set; }
    public decimal? GrandTotal { get; set; }
    public decimal? DiscountTotal { get; set; }
}

public class BranchLookupDto
{
    public string BranchId { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
}

public class RetryTalabatOrderLogsInput
{
    public string? VendorCode { get; set; }
    public bool IncludeEnqueued { get; set; } = true;
}

public class RetryTalabatOrderLogsResultDto
{
    public int QueuedCount { get; set; }
    public int SkippedCount { get; set; }
}

public class TalabatOrderDetailsDto
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public string? OrderToken { get; set; }
    public string? ShortCode { get; set; }
    public string? VendorCode { get; set; }
    public string? Status { get; set; }
    public DateTime? OrderCreatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ExpeditionType { get; set; }
    public string? Channel { get; set; }
    public decimal? GrandTotal { get; set; }
    public decimal? DiscountTotal { get; set; }
    public string? CustomerComment { get; set; }
    public List<TalabatOrderItemDto> Items { get; set; } = [];
    public string? LastError { get; set; }
    public int Attempts { get; set; }
    public string? FoodicsOrderId { get; set; }
    /// <summary>The raw Talabat webhook JSON as received (for the full-payload view).</summary>
    public string? RawPayloadJson { get; set; }
}

public class TalabatOrderItemDto
{
    public string? Name { get; set; }
    public string? CategoryName { get; set; }
    public string? RemoteCode { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? PaidPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public List<TalabatOrderItemDiscountDto> Discounts { get; set; } = [];
    public List<TalabatOrderModifierDto> Modifiers { get; set; } = [];
}

public class TalabatOrderModifierDto
{
    public string? Name { get; set; }
    public string? RemoteCode { get; set; }
    public int Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? DiscountAmount { get; set; }
    public List<TalabatOrderItemDiscountDto> Discounts { get; set; } = [];
}

public class TalabatOrderItemDiscountDto
{
    public string? Name { get; set; }
    public decimal? Amount { get; set; }
    public List<TalabatOrderDiscountSponsorshipDto> Sponsorships { get; set; } = [];
}

public class TalabatOrderDiscountSponsorshipDto
{
    public string? Sponsor { get; set; }
    public decimal? Amount { get; set; }
}
