using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Foodics;

namespace OrderXChange.Domain.Staging;

/// <summary>
/// Staging table for Foodics products data.
/// Stores product data fetched from Foodics API for each FoodicsAccount.
/// This table acts as a staging area before syncing to Talabat.
/// </summary>
public class FoodicsProductStaging : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	/// <summary>
	/// Foreign key to FoodicsAccount - identifies which Foodics account this product belongs to
	/// </summary>
	[Required]

	public Guid FoodicsAccountId { get; set; }

	/// <summary>
	/// Product ID from Foodics system (unique within FoodicsAccount)
	/// </summary>
	[Required]
	[MaxLength(100)]
	public string FoodicsProductId { get; set; } = string.Empty;

	/// <summary>
	/// Product name
	/// </summary>
	[Required]
	[MaxLength(500)]
	public string Name { get; set; } = string.Empty;

	[MaxLength(500)]
	public string? NameLocalized { get; set; }

	/// <summary>
	/// Product description (HTML supported)
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? Description { get; set; }

	[Column(TypeName = "TEXT")]
	public string? DescriptionLocalized { get; set; }

	/// <summary>
	/// Product image URL
	/// </summary>
	[MaxLength(1000)]
	public string? Image { get; set; }

	/// <summary>
	/// Whether product is active in Foodics
	/// </summary>
	public bool IsActive { get; set; }

	/// <summary>
	/// Stock Keeping Unit
	/// </summary>
	[MaxLength(100)]
	public string? Sku { get; set; }

	/// <summary>
	/// Product barcode
	/// </summary>
	[MaxLength(100)]
	public string? Barcode { get; set; }

	/// <summary>
	/// Category ID from Foodics
	/// </summary>
	[MaxLength(100)]
	public string? CategoryId { get; set; }

	/// <summary>
	/// Category name (denormalized for quick access)
	/// </summary>
	[MaxLength(500)]
	public string? CategoryName { get; set; }

	/// <summary>
	/// Tax group ID from Foodics
	/// </summary>
	[MaxLength(100)]
	public string? TaxGroupId { get; set; }

	/// <summary>
	/// Tax group name (denormalized)
	/// </summary>
	[MaxLength(500)]
	public string? TaxGroupName { get; set; }

	/// <summary>
	/// Tax rate percentage
	/// </summary>
	[Column(TypeName = "DECIMAL(5,2)")]
	public decimal? TaxRate { get; set; }

	/// <summary>
	/// Product base price
	/// </summary>
	[Column(TypeName = "DECIMAL(18,2)")]
	public decimal? Price { get; set; }

	/// <summary>
	/// JSON serialized full product details (includes: price_tags, tax_group, tags, branches, modifiers, groups, etc.)
	/// Stores complete product data for reference and future use
	/// </summary>
	[Column(TypeName = "LONGTEXT")]
	public string? ProductDetailsJson { get; set; }

	/// <summary>
	/// JSON serialized branches array - which branches this product is available in
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? BranchesJson { get; set; }

	/// <summary>
	/// JSON serialized modifiers array - product modifiers with options
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? ModifiersJson { get; set; }

	/// <summary>
	/// JSON serialized groups array - custom groups this product belongs to
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? GroupsJson { get; set; }

	/// <summary>
	/// JSON serialized price tags array - different prices for different tags
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? PriceTagsJson { get; set; }

	/// <summary>
	/// JSON serialized tags array
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? TagsJson { get; set; }

	/// <summary>
	/// JSON serialized ingredients array
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? IngredientsJson { get; set; }

	/// <summary>
	/// JSON serialized discounts array
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? DiscountsJson { get; set; }

	/// <summary>
	/// JSON serialized timed events array
	/// </summary>
	[Column(TypeName = "TEXT")]
	public string? TimedEventsJson { get; set; }

	/// <summary>
	/// Last sync date from Foodics - when this product was last fetched/updated
	/// </summary>
	public DateTime SyncDate { get; set; }

	/// <summary>
	/// Branch ID if synced for specific branch (nullable for all branches sync)
	/// </summary>
	[MaxLength(100)]
	public string? BranchId { get; set; }

	/// <summary>
	/// Tenant ID - for multi-tenancy support
	/// </summary>
	public Guid? TenantId { get; set; }

	#region Talabat Sync Status Fields

	/// <summary>
	/// Talabat sync status: Pending, Submitted, Success, Failed, Partial
	/// </summary>
	[MaxLength(50)]
	public string? TalabatSyncStatus { get; set; }

	/// <summary>
	/// Talabat catalog import ID returned after submission
	/// </summary>
	[MaxLength(200)]
	public string? TalabatImportId { get; set; }

	/// <summary>
	/// When the product was last submitted to Talabat
	/// </summary>
	public DateTime? TalabatSubmittedAt { get; set; }

	/// <summary>
	/// When Talabat confirmed the sync (via webhook)
	/// </summary>
	public DateTime? TalabatSyncCompletedAt { get; set; }

	/// <summary>
	/// Last error message from Talabat sync (if any)
	/// </summary>
	[MaxLength(2000)]
	public string? TalabatLastError { get; set; }

	/// <summary>
	/// Talabat vendor code this product was synced to
	/// </summary>
	[MaxLength(100)]
	public string? TalabatVendorCode { get; set; }

	#endregion

	#region Soft Delete Fields

	/// <summary>
	/// Indicates if this product is soft deleted (removed from Foodics but kept for audit)
	/// </summary>
	public bool IsDeleted { get; set; }

	/// <summary>
	/// When this product was marked as deleted
	/// </summary>
	public DateTime? DeletedAt { get; set; }

	/// <summary>
	/// Reason for deletion: RemovedFromFoodics, ManuallyDeleted, BranchUnavailable, etc.
	/// </summary>
	[MaxLength(100)]
	public string? DeletionReason { get; set; }

	/// <summary>
	/// User/System that marked this product as deleted
	/// </summary>
	[MaxLength(200)]
	public string? DeletedBy { get; set; }

	/// <summary>
	/// Whether the deletion has been synced to Talabat
	/// </summary>
	public bool IsDeletionSyncedToTalabat { get; set; }

	/// <summary>
	/// When the deletion was synced to Talabat
	/// </summary>
	public DateTime? DeletionSyncedAt { get; set; }

	/// <summary>
	/// Last error when trying to sync deletion to Talabat
	/// </summary>
	[MaxLength(2000)]
	public string? DeletionSyncError { get; set; }

	#endregion

	// Navigation properties
	public virtual FoodicsAccount FoodicsAccount { get; set; }
}

