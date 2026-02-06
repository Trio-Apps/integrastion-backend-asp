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

	public Guid? TenantId { get; set; }

	public virtual FoodicsAccount FoodicsAccount { get; set; } = null!;
}
