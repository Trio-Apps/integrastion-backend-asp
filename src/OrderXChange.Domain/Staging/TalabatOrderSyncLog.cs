using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Foodics;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Staging;

/// <summary>
/// Tracks Talabat order webhook processing and Foodics order dispatch status.
/// </summary>
public class TalabatOrderSyncLog : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	[Required]
	public Guid FoodicsAccountId { get; set; }

	[Required]
	[MaxLength(100)]
	public string VendorCode { get; set; } = string.Empty;

	[MaxLength(100)]
	public string? PlatformRestaurantId { get; set; }

	[MaxLength(100)]
	public string? OrderToken { get; set; }

	[MaxLength(100)]
	public string? OrderCode { get; set; }

	[MaxLength(50)]
	public string? ShortCode { get; set; }

	[MaxLength(50)]
	public string Status { get; set; } = "Received";

	[MaxLength(100)]
	public string? CorrelationId { get; set; }

	public bool IsTestOrder { get; set; }

	public int ProductsCount { get; set; }

	public int CategoriesCount { get; set; }

	public DateTime? OrderCreatedAt { get; set; }

	public DateTime ReceivedAt { get; set; }

	public DateTime? CompletedAt { get; set; }

	public int Attempts { get; set; }

	public DateTime? LastAttemptUtc { get; set; }

	[Column(TypeName = "TEXT")]
	public string? ErrorMessage { get; set; }

	[MaxLength(200)]
	public string? ErrorCode { get; set; }

	[MaxLength(100)]
	public string? FoodicsOrderId { get; set; }

	[Column(TypeName = "LONGTEXT")]
	public string? FoodicsResponseJson { get; set; }

	[Column(TypeName = "LONGTEXT")]
	public string? WebhookPayloadJson { get; set; }

	// Listing fields parsed from the webhook at receipt (§2 Orders Console)
	[MaxLength(100)]
	public string? CustomerId { get; set; }

	[MaxLength(200)]
	public string? CustomerName { get; set; }

	[MaxLength(50)]
	public string? CustomerPhone { get; set; }

	[MaxLength(500)]
	public string? CustomerAddress { get; set; }

	[MaxLength(100)]
	public string? PaymentMethod { get; set; }

	/// <summary>ExpeditionType from Talabat (e.g. "TMP" = platform delivery, "TGO" = vendor delivery).</summary>
	[MaxLength(100)]
	public string? ExpeditionType { get; set; }

	/// <summary>Talabat platform/market channel (e.g. "q8", "ae") from localInfo.platformKey.</summary>
	[MaxLength(100)]
	public string? Channel { get; set; }

	public decimal? GrandTotal { get; set; }

	public decimal? DiscountTotal { get; set; }

	public Guid? TenantId { get; set; }

	public virtual FoodicsAccount FoodicsAccount { get; set; } = null!;
}
