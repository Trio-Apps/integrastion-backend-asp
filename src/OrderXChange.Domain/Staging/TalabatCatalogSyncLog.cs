using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Foodics;

namespace OrderXChange.Domain.Staging;

/// <summary>
/// Tracks Talabat catalog sync operations and their status.
/// Updated by webhooks when Talabat confirms import status.
/// </summary>
public class TalabatCatalogSyncLog : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	/// <summary>
	/// Foreign key to FoodicsAccount
	/// </summary>
	[Required]
	public Guid FoodicsAccountId { get; set; }

	/// <summary>
	/// Talabat vendor code (branch identifier in Talabat)
	/// </summary>
	[Required]
	[MaxLength(100)]
	public string VendorCode { get; set; } = string.Empty;

	/// <summary>
	/// Talabat chain code (for V2 API)
	/// </summary>
	[MaxLength(100)]
	public string? ChainCode { get; set; }

	/// <summary>
	/// Talabat catalog import ID returned after submission
	/// </summary>
	[MaxLength(200)]
	public string? ImportId { get; set; }

	/// <summary>
	/// Correlation ID for tracking the request
	/// </summary>
	[MaxLength(100)]
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Current status: Submitted, Processing, Done, Failed, Partial
	/// </summary>
	[Required]
	[MaxLength(50)]
	public string Status { get; set; } = "Submitted";

	/// <summary>
	/// API version used: V1 or V2
	/// </summary>
	[MaxLength(10)]
	public string? ApiVersion { get; set; }

	/// <summary>
	/// Number of categories in the catalog
	/// </summary>
	public int CategoriesCount { get; set; }

	/// <summary>
	/// Number of products in the catalog
	/// </summary>
	public int ProductsCount { get; set; }

	/// <summary>
	/// Number of categories created (from webhook)
	/// </summary>
	public int CategoriesCreated { get; set; }

	/// <summary>
	/// Number of categories updated (from webhook)
	/// </summary>
	public int CategoriesUpdated { get; set; }

	/// <summary>
	/// Number of products created (from webhook)
	/// </summary>
	public int ProductsCreated { get; set; }

	/// <summary>
	/// Number of products updated (from webhook)
	/// </summary>
	public int ProductsUpdated { get; set; }

	/// <summary>
	/// Number of errors during import
	/// </summary>
	public int ErrorsCount { get; set; }

	/// <summary>
	/// JSON serialized errors array from webhook
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? ErrorsJson { get; set; }

	/// <summary>
	/// Full response message from Talabat
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? ResponseMessage { get; set; }

	/// <summary>
	/// When the catalog was submitted to Talabat
	/// </summary>
	public DateTime SubmittedAt { get; set; }

	/// <summary>
	/// When Talabat confirmed the import (via webhook)
	/// </summary>
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	/// Duration in seconds from submission to completion
	/// </summary>
	public int? ProcessingDurationSeconds { get; set; }

	/// <summary>
	/// Callback URL used for this submission
	/// </summary>
	[MaxLength(500)]
	public string? CallbackUrl { get; set; }

	/// <summary>
	/// Raw webhook payload received (for debugging)
	/// </summary>
	[Column(TypeName = "LONGTEXT")]
	public string? WebhookPayloadJson { get; set; }

	/// <summary>
	/// V2 API: Details array from webhook (per-vendor status)
	/// Serialized JSON of TalabatCatalogStatusDetail list
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? DetailsJson { get; set; }

	/// <summary>
	/// Tenant ID for multi-tenancy
	/// </summary>
	public Guid? TenantId { get; set; }

	// Navigation properties
	public virtual FoodicsAccount FoodicsAccount { get; set; } = null!;
}

