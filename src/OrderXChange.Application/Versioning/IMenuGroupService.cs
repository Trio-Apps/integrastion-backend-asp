using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Versioning;
using Volo.Abp.Application.Services;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service interface for managing Menu Groups
/// Provides operations for creating, updating, and managing Menu Groups and their category assignments
/// </summary>
public interface IMenuGroupService : IApplicationService
{
    /// <summary>
    /// Creates a new Menu Group
    /// </summary>
    Task<MenuGroupDto> CreateAsync(CreateMenuGroupDto input);

    /// <summary>
    /// Updates an existing Menu Group
    /// </summary>
    Task<MenuGroupDto> UpdateAsync(Guid id, UpdateMenuGroupDto input);

    /// <summary>
    /// Gets a Menu Group by ID
    /// </summary>
    Task<MenuGroupDto> GetAsync(Guid id);

    /// <summary>
    /// Gets all Menu Groups for a Foodics account and branch
    /// </summary>
    Task<List<MenuGroupDto>> GetByAccountAndBranchAsync(Guid foodicsAccountId, string? branchId = null);

    /// <summary>
    /// Gets all active Menu Groups for a Foodics account and branch
    /// </summary>
    Task<List<MenuGroupDto>> GetActiveByAccountAndBranchAsync(Guid foodicsAccountId, string? branchId = null);

    /// <summary>
    /// Deletes a Menu Group (soft delete)
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Activates a Menu Group
    /// </summary>
    Task ActivateAsync(Guid id);

    /// <summary>
    /// Deactivates a Menu Group
    /// </summary>
    Task DeactivateAsync(Guid id);

    /// <summary>
    /// Assigns a category to a Menu Group
    /// </summary>
    Task<MenuGroupCategoryDto> AssignCategoryAsync(Guid menuGroupId, AssignCategoryDto input);

    /// <summary>
    /// Removes a category from a Menu Group
    /// </summary>
    Task RemoveCategoryAsync(Guid menuGroupId, string categoryId);

    /// <summary>
    /// Gets all categories assigned to a Menu Group
    /// </summary>
    Task<List<MenuGroupCategoryDto>> GetCategoriesAsync(Guid menuGroupId);

    /// <summary>
    /// Updates the sort order of categories within a Menu Group
    /// </summary>
    Task UpdateCategorySortOrderAsync(Guid menuGroupId, List<CategorySortOrderDto> sortOrders);

    /// <summary>
    /// Validates a Menu Group for sync readiness
    /// </summary>
    Task<MenuGroupValidationResultDto> ValidateForSyncAsync(Guid id);

    /// <summary>
    /// Gets Menu Group statistics (product counts, category counts, etc.)
    /// </summary>
    Task<MenuGroupStatisticsDto> GetStatisticsAsync(Guid id);

    /// <summary>
    /// Finds Menu Groups that contain a specific category
    /// </summary>
    Task<List<MenuGroupDto>> FindByCategory(Guid foodicsAccountId, string categoryId);

    /// <summary>
    /// Duplicates a Menu Group with all its category assignments
    /// </summary>
    Task<MenuGroupDto> DuplicateAsync(Guid id, string newName);
}