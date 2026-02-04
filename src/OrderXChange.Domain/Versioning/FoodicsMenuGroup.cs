using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Represents a logical menu grouping within a Foodics branch
/// Enables multi-brand operations and flexible menu organization
/// </summary>
public class FoodicsMenuGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    [Required]
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID from Foodics (null for account-level Menu Groups)
    /// </summary>
    [MaxLength(100)]
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group name (unique within branch)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the Menu Group
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this Menu Group is active and available for sync
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Sort order for Menu Group display
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// JSON metadata for flexible configuration
    /// Can store brand-specific settings, sync preferences, etc.
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual Foodics.FoodicsAccount FoodicsAccount { get; set; } = null!;
    public virtual ICollection<MenuGroupCategory> Categories { get; set; } = new List<MenuGroupCategory>();
    public virtual ICollection<MenuSnapshot> Snapshots { get; set; } = new List<MenuSnapshot>();
    public virtual ICollection<MenuSyncRun> SyncRuns { get; set; } = new List<MenuSyncRun>();
    public virtual ICollection<MenuItemMapping> ItemMappings { get; set; } = new List<MenuItemMapping>();

    #region Business Methods

    /// <summary>
    /// Adds a category to this Menu Group
    /// </summary>
    public MenuGroupCategory AddCategory(string categoryId, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            throw new ArgumentException("Category ID cannot be empty", nameof(categoryId));

        // Check if category is already assigned
        var existingAssignment = Categories.FirstOrDefault(c => c.CategoryId == categoryId && c.IsActive);
        if (existingAssignment != null)
        {
            // Update sort order if different
            if (existingAssignment.SortOrder != sortOrder)
            {
                existingAssignment.UpdateSortOrder(sortOrder);
            }
            return existingAssignment;
        }

        var assignment = new MenuGroupCategory
        {
            MenuGroupId = Id,
            CategoryId = categoryId,
            SortOrder = sortOrder,
            IsActive = true,
            AssignedAt = DateTime.UtcNow,
            TenantId = TenantId
        };

        Categories.Add(assignment);
        return assignment;
    }

    /// <summary>
    /// Removes a category from this Menu Group
    /// </summary>
    public void RemoveCategory(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            throw new ArgumentException("Category ID cannot be empty", nameof(categoryId));

        var assignment = Categories.FirstOrDefault(c => c.CategoryId == categoryId && c.IsActive);
        if (assignment != null)
        {
            assignment.Deactivate();
        }
    }

    /// <summary>
    /// Gets all active category IDs assigned to this Menu Group
    /// </summary>
    public List<string> GetActiveCategoryIds()
    {
        return Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.AssignedAt)
            .Select(c => c.CategoryId)
            .ToList();
    }

    /// <summary>
    /// Checks if this Menu Group contains the specified category
    /// </summary>
    public bool ContainsCategory(string categoryId)
    {
        return Categories.Any(c => c.CategoryId == categoryId && c.IsActive);
    }

    /// <summary>
    /// Updates the Menu Group metadata
    /// </summary>
    public void UpdateMetadata(string? metadataJson)
    {
        MetadataJson = metadataJson;
    }

    /// <summary>
    /// Activates the Menu Group
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivates the Menu Group
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Updates the sort order
    /// </summary>
    public void UpdateSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Validates that the Menu Group is ready for sync
    /// </summary>
    public MenuGroupValidationResult ValidateForSync()
    {
        var result = new MenuGroupValidationResult { IsValid = true };

        if (!IsActive)
        {
            result.IsValid = false;
            result.Errors.Add("Menu Group is not active");
        }

        var activeCategoryIds = GetActiveCategoryIds();
        if (!activeCategoryIds.Any())
        {
            result.IsValid = false;
            result.Errors.Add("Menu Group must contain at least one active category");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            result.IsValid = false;
            result.Errors.Add("Menu Group must have a name");
        }

        return result;
    }

    /// <summary>
    /// Gets a unique identifier for this Menu Group context
    /// Used for scoped operations like snapshots and mappings
    /// </summary>
    public string GetContextKey()
    {
        return $"{FoodicsAccountId}:{BranchId ?? "ALL"}:{Id}";
    }

    #endregion
}

/// <summary>
/// Validation result for Menu Group operations
/// </summary>
public class MenuGroupValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}