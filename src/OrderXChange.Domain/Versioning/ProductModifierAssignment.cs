using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Tracks which modifier groups are assigned to which products
/// Enables efficient querying and change detection for product-modifier relationships
/// </summary>
public class ProductModifierAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    [Required]
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID from Foodics
    /// </summary>
    [MaxLength(100)]
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group ID for scoped assignments
    /// </summary>
    public Guid? MenuGroupId { get; set; }

    /// <summary>
    /// Foodics product ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FoodicsProductId { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to ModifierGroup
    /// </summary>
    [Required]
    public Guid ModifierGroupId { get; set; }

    /// <summary>
    /// Display order of this modifier group on the product
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this assignment is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this assignment was created in Foodics
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// When this assignment was last updated
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// Whether this assignment has been synced to Talabat
    /// </summary>
    public bool IsSyncedToTalabat { get; set; }

    /// <summary>
    /// When this assignment was last synced to Talabat
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Talabat vendor code this assignment was synced to
    /// </summary>
    [MaxLength(100)]
    public string? TalabatVendorCode { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual Foodics.FoodicsAccount FoodicsAccount { get; set; } = null!;
    public virtual FoodicsMenuGroup? MenuGroup { get; set; }
    public virtual ModifierGroup ModifierGroup { get; set; } = null!;

    #region Business Methods

    /// <summary>
    /// Activates the assignment
    /// </summary>
    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false;
        }
    }

    /// <summary>
    /// Deactivates the assignment
    /// </summary>
    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false;
        }
    }

    /// <summary>
    /// Updates the sort order
    /// </summary>
    public void UpdateSortOrder(int newSortOrder)
    {
        if (SortOrder != newSortOrder)
        {
            SortOrder = newSortOrder;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false;
        }
    }

    /// <summary>
    /// Marks as synced to Talabat
    /// </summary>
    public void MarkAsSynced(string talabatVendorCode)
    {
        IsSyncedToTalabat = true;
        LastSyncedAt = DateTime.UtcNow;
        TalabatVendorCode = talabatVendorCode;
    }

    /// <summary>
    /// Gets the unique context key for this assignment
    /// </summary>
    public string GetContextKey()
    {
        return $"{FoodicsAccountId}:{BranchId ?? "ALL"}:{MenuGroupId?.ToString() ?? "ALL"}:{FoodicsProductId}:{ModifierGroupId}";
    }

    #endregion
}