using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Association entity between Menu Groups and Categories
/// Supports many-to-many relationship with ordering and activation tracking
/// </summary>
public class MenuGroupCategory : FullAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsMenuGroup
    /// </summary>
    [Required]
    public Guid MenuGroupId { get; set; }

    /// <summary>
    /// Category ID from Foodics
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this category assignment is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Sort order within the Menu Group
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// When this category was assigned to the Menu Group
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual FoodicsMenuGroup MenuGroup { get; set; } = null!;

    #region Business Methods

    /// <summary>
    /// Updates the sort order for this category assignment
    /// </summary>
    public void UpdateSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Activates this category assignment
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivates this category assignment (soft delete)
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Checks if this assignment is currently effective
    /// </summary>
    public bool IsEffective()
    {
        return IsActive && !IsDeleted;
    }

    /// <summary>
    /// Gets the assignment age in days
    /// </summary>
    public int GetAssignmentAgeDays()
    {
        return (DateTime.UtcNow - AssignedAt).Days;
    }

    #endregion
}