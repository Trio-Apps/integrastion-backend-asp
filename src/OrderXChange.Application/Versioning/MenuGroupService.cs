using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Versioning;
using OrderXChange.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service implementation for managing Menu Groups
/// Provides comprehensive Menu Group management with validation and business logic
/// </summary>
public class MenuGroupService : ApplicationService, IMenuGroupService
{
    private readonly IRepository<FoodicsMenuGroup, Guid> _menuGroupRepository;
    private readonly IRepository<MenuGroupCategory, Guid> _menuGroupCategoryRepository;
    private readonly IRepository<MenuSyncRun, Guid> _menuSyncRunRepository;
    private readonly OrderXChangeDbContext _dbContext;
    private readonly ILogger<MenuGroupService> _logger;

    public MenuGroupService(
        IRepository<FoodicsMenuGroup, Guid> menuGroupRepository,
        IRepository<MenuGroupCategory, Guid> menuGroupCategoryRepository,
        IRepository<MenuSyncRun, Guid> menuSyncRunRepository,
        OrderXChangeDbContext dbContext,
        ILogger<MenuGroupService> logger)
    {
        _menuGroupRepository = menuGroupRepository;
        _menuGroupCategoryRepository = menuGroupCategoryRepository;
        _menuSyncRunRepository = menuSyncRunRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MenuGroupDto> CreateAsync(CreateMenuGroupDto input)
    {
        _logger.LogInformation("Creating Menu Group: {Name} for Account: {AccountId}, Branch: {BranchId}", 
            input.Name, input.FoodicsAccountId, input.BranchId);

        // Validate unique name within account/branch
        await ValidateUniqueNameAsync(input.FoodicsAccountId, input.BranchId, input.Name);

        var menuGroup = new FoodicsMenuGroup
        {
            FoodicsAccountId = input.FoodicsAccountId,
            BranchId = input.BranchId,
            Name = input.Name,
            Description = input.Description,
            SortOrder = input.SortOrder,
            MetadataJson = input.MetadataJson,
            IsActive = true
        };

        // Add initial categories if provided
        foreach (var categoryId in input.CategoryIds)
        {
            menuGroup.AddCategory(categoryId);
        }

        var createdMenuGroup = await _menuGroupRepository.InsertAsync(menuGroup, autoSave: true);

        _logger.LogInformation("Created Menu Group: {Id} with {CategoryCount} categories", 
            createdMenuGroup.Id, input.CategoryIds.Count);

        return await MapToMenuGroupDtoAsync(createdMenuGroup);
    }

    public async Task<MenuGroupDto> UpdateAsync(Guid id, UpdateMenuGroupDto input)
    {
        _logger.LogInformation("Updating Menu Group: {Id}", id);

        var menuGroup = await _menuGroupRepository.GetAsync(id);

        // Validate unique name if changed
        if (menuGroup.Name != input.Name)
        {
            await ValidateUniqueNameAsync(menuGroup.FoodicsAccountId, menuGroup.BranchId, input.Name, id);
        }

        menuGroup.Name = input.Name;
        menuGroup.Description = input.Description;
        menuGroup.SortOrder = input.SortOrder;
        menuGroup.UpdateMetadata(input.MetadataJson);

        var updatedMenuGroup = await _menuGroupRepository.UpdateAsync(menuGroup, autoSave: true);

        _logger.LogInformation("Updated Menu Group: {Id}", id);

        return await MapToMenuGroupDtoAsync(updatedMenuGroup);
    }

    public async Task<MenuGroupDto> GetAsync(Guid id)
    {
        var menuGroup = await _menuGroupRepository.GetAsync(id);
        return await MapToMenuGroupDtoAsync(menuGroup);
    }

    public async Task<List<MenuGroupDto>> GetByAccountAndBranchAsync(Guid foodicsAccountId, string? branchId = null)
    {
        var queryable = await _menuGroupRepository.GetQueryableAsync();
        
        var query = queryable
            .Where(mg => mg.FoodicsAccountId == foodicsAccountId)
            .Where(mg => mg.BranchId == branchId)
            .OrderBy(mg => mg.SortOrder)
            .ThenBy(mg => mg.Name);

        var menuGroups = await AsyncExecuter.ToListAsync(query);
        
        var result = new List<MenuGroupDto>();
        foreach (var menuGroup in menuGroups)
        {
            result.Add(await MapToMenuGroupDtoAsync(menuGroup));
        }

        return result;
    }

    public async Task<List<MenuGroupDto>> GetActiveByAccountAndBranchAsync(Guid foodicsAccountId, string? branchId = null)
    {
        var queryable = await _menuGroupRepository.GetQueryableAsync();
        
        var query = queryable
            .Where(mg => mg.FoodicsAccountId == foodicsAccountId)
            .Where(mg => mg.BranchId == branchId)
            .Where(mg => mg.IsActive)
            .OrderBy(mg => mg.SortOrder)
            .ThenBy(mg => mg.Name);

        var menuGroups = await AsyncExecuter.ToListAsync(query);
        
        var result = new List<MenuGroupDto>();
        foreach (var menuGroup in menuGroups)
        {
            result.Add(await MapToMenuGroupDtoAsync(menuGroup));
        }

        return result;
    }

    public async Task DeleteAsync(Guid id)
    {
        _logger.LogInformation("Deleting Menu Group: {Id}", id);

        var menuGroup = await _menuGroupRepository.GetAsync(id);
        
        // Check if Menu Group has any active sync runs
        var activeSyncRuns = await _menuSyncRunRepository.CountAsync(sr => 
            sr.MenuGroupId == id && 
            (sr.Status == MenuSyncRunStatus.Running || sr.Status == MenuSyncRunStatus.Pending));

        if (activeSyncRuns > 0)
        {
            throw new BusinessException("Cannot delete Menu Group with active sync runs");
        }

        await _menuGroupRepository.DeleteAsync(menuGroup, autoSave: true);

        _logger.LogInformation("Deleted Menu Group: {Id}", id);
    }

    public async Task ActivateAsync(Guid id)
    {
        _logger.LogInformation("Activating Menu Group: {Id}", id);

        var menuGroup = await _menuGroupRepository.GetAsync(id);
        menuGroup.Activate();
        
        await _menuGroupRepository.UpdateAsync(menuGroup, autoSave: true);

        _logger.LogInformation("Activated Menu Group: {Id}", id);
    }

    public async Task DeactivateAsync(Guid id)
    {
        _logger.LogInformation("Deactivating Menu Group: {Id}", id);

        var menuGroup = await _menuGroupRepository.GetAsync(id);
        menuGroup.Deactivate();
        
        await _menuGroupRepository.UpdateAsync(menuGroup, autoSave: true);

        _logger.LogInformation("Deactivated Menu Group: {Id}", id);
    }

    public async Task<MenuGroupCategoryDto> AssignCategoryAsync(Guid menuGroupId, AssignCategoryDto input)
    {
        _logger.LogInformation("Assigning category {CategoryId} to Menu Group: {MenuGroupId}", 
            input.CategoryId, menuGroupId);

        var menuGroup = await _menuGroupRepository.GetAsync(menuGroupId);
        var categoryAssignment = menuGroup.AddCategory(input.CategoryId, input.SortOrder);
        
        await _menuGroupRepository.UpdateAsync(menuGroup, autoSave: true);

        _logger.LogInformation("Assigned category {CategoryId} to Menu Group: {MenuGroupId}", 
            input.CategoryId, menuGroupId);

        return ObjectMapper.Map<MenuGroupCategory, MenuGroupCategoryDto>(categoryAssignment);
    }

    public async Task RemoveCategoryAsync(Guid menuGroupId, string categoryId)
    {
        _logger.LogInformation("Removing category {CategoryId} from Menu Group: {MenuGroupId}", 
            categoryId, menuGroupId);

        var menuGroup = await _menuGroupRepository.GetAsync(menuGroupId);
        menuGroup.RemoveCategory(categoryId);
        
        await _menuGroupRepository.UpdateAsync(menuGroup, autoSave: true);

        _logger.LogInformation("Removed category {CategoryId} from Menu Group: {MenuGroupId}", 
            categoryId, menuGroupId);
    }

    public async Task<List<MenuGroupCategoryDto>> GetCategoriesAsync(Guid menuGroupId)
    {
        var queryable = await _menuGroupCategoryRepository.GetQueryableAsync();
        
        var categories = await AsyncExecuter.ToListAsync(
            queryable
                .Where(mgc => mgc.MenuGroupId == menuGroupId && mgc.IsActive)
                .OrderBy(mgc => mgc.SortOrder)
                .ThenBy(mgc => mgc.AssignedAt)
        );

        return ObjectMapper.Map<List<MenuGroupCategory>, List<MenuGroupCategoryDto>>(categories);
    }

    public async Task UpdateCategorySortOrderAsync(Guid menuGroupId, List<CategorySortOrderDto> sortOrders)
    {
        _logger.LogInformation("Updating category sort order for Menu Group: {MenuGroupId}", menuGroupId);

        var queryable = await _menuGroupCategoryRepository.GetQueryableAsync();
        var categories = await AsyncExecuter.ToListAsync(
            queryable.Where(mgc => mgc.MenuGroupId == menuGroupId && mgc.IsActive)
        );

        foreach (var sortOrder in sortOrders)
        {
            var category = categories.FirstOrDefault(c => c.CategoryId == sortOrder.CategoryId);
            if (category != null)
            {
                category.UpdateSortOrder(sortOrder.SortOrder);
            }
        }

        await _menuGroupCategoryRepository.UpdateManyAsync(categories, autoSave: true);

        _logger.LogInformation("Updated category sort order for Menu Group: {MenuGroupId}", menuGroupId);
    }

    public async Task<MenuGroupValidationResultDto> ValidateForSyncAsync(Guid id)
    {
        var menuGroup = await _menuGroupRepository.GetAsync(id);
        var validationResult = menuGroup.ValidateForSync();

        return new MenuGroupValidationResultDto
        {
            IsValid = validationResult.IsValid,
            Errors = validationResult.Errors,
            Warnings = validationResult.Warnings
        };
    }

    public async Task<MenuGroupStatisticsDto> GetStatisticsAsync(Guid id)
    {
        var menuGroup = await _menuGroupRepository.GetAsync(id);
        
        // Get category statistics
        var categoryQueryable = await _menuGroupCategoryRepository.GetQueryableAsync();
        var categories = await AsyncExecuter.ToListAsync(
            categoryQueryable.Where(mgc => mgc.MenuGroupId == id)
        );

        var activeCategories = categories.Where(c => c.IsActive).ToList();

        // Get sync statistics
        var syncQueryable = await _menuSyncRunRepository.GetQueryableAsync();
        var syncRuns = await AsyncExecuter.ToListAsync(
            syncQueryable.Where(sr => sr.MenuGroupId == id && sr.IsCompleted)
        );

        var successfulSyncs = syncRuns.Count(sr => sr.Status == MenuSyncRunStatus.Completed);
        var failedSyncs = syncRuns.Count(sr => sr.Status == MenuSyncRunStatus.Failed);
        var lastSyncDate = syncRuns.OrderByDescending(sr => sr.StartedAt).FirstOrDefault()?.StartedAt;
        var avgDuration = syncRuns.Where(sr => sr.Duration.HasValue).Average(sr => sr.Duration!.Value.TotalSeconds);

        return new MenuGroupStatisticsDto
        {
            MenuGroupId = id,
            TotalCategories = categories.Count,
            ActiveCategories = activeCategories.Count,
            SuccessfulSyncs = successfulSyncs,
            FailedSyncs = failedSyncs,
            LastSyncDate = lastSyncDate,
            AverageSyncDuration = avgDuration > 0 ? TimeSpan.FromSeconds(avgDuration) : null,
            CategoryStatistics = activeCategories.Select(c => new CategoryStatisticsDto
            {
                CategoryId = c.CategoryId,
                LastUpdated = c.LastModificationTime
            }).ToList()
        };
    }

    public async Task<List<MenuGroupDto>> FindByCategory(Guid foodicsAccountId, string categoryId)
    {
        var categoryQueryable = await _menuGroupCategoryRepository.GetQueryableAsync();
        var menuGroupQueryable = await _menuGroupRepository.GetQueryableAsync();

        var query = from mgc in categoryQueryable
                   join mg in menuGroupQueryable on mgc.MenuGroupId equals mg.Id
                   where mgc.CategoryId == categoryId 
                         && mgc.IsActive 
                         && mg.FoodicsAccountId == foodicsAccountId
                         && mg.IsActive
                   select mg;

        var menuGroups = await AsyncExecuter.ToListAsync(query);
        
        var result = new List<MenuGroupDto>();
        foreach (var menuGroup in menuGroups)
        {
            result.Add(await MapToMenuGroupDtoAsync(menuGroup));
        }

        return result;
    }

    public async Task<MenuGroupDto> DuplicateAsync(Guid id, string newName)
    {
        _logger.LogInformation("Duplicating Menu Group: {Id} with new name: {NewName}", id, newName);

        var originalMenuGroup = await _menuGroupRepository.GetAsync(id);
        
        // Validate unique name
        await ValidateUniqueNameAsync(originalMenuGroup.FoodicsAccountId, originalMenuGroup.BranchId, newName);

        var duplicatedMenuGroup = new FoodicsMenuGroup
        {
            FoodicsAccountId = originalMenuGroup.FoodicsAccountId,
            BranchId = originalMenuGroup.BranchId,
            Name = newName,
            Description = $"Copy of {originalMenuGroup.Name}",
            SortOrder = originalMenuGroup.SortOrder + 1,
            MetadataJson = originalMenuGroup.MetadataJson,
            IsActive = true
        };

        // Copy all active categories
        var activeCategoryIds = originalMenuGroup.GetActiveCategoryIds();
        foreach (var categoryId in activeCategoryIds)
        {
            var originalCategory = originalMenuGroup.Categories.First(c => c.CategoryId == categoryId && c.IsActive);
            duplicatedMenuGroup.AddCategory(categoryId, originalCategory.SortOrder);
        }

        var createdMenuGroup = await _menuGroupRepository.InsertAsync(duplicatedMenuGroup, autoSave: true);

        _logger.LogInformation("Duplicated Menu Group: {OriginalId} -> {NewId}", id, createdMenuGroup.Id);

        return await MapToMenuGroupDtoAsync(createdMenuGroup);
    }

    #region Private Methods

    private async Task ValidateUniqueNameAsync(Guid foodicsAccountId, string? branchId, string name, Guid? excludeId = null)
    {
        var queryable = await _menuGroupRepository.GetQueryableAsync();
        
        var query = queryable
            .Where(mg => mg.FoodicsAccountId == foodicsAccountId)
            .Where(mg => mg.BranchId == branchId)
            .Where(mg => mg.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(mg => mg.Id != excludeId.Value);
        }

        var exists = await AsyncExecuter.AnyAsync(query);
        
        if (exists)
        {
            throw new BusinessException($"Menu Group with name '{name}' already exists in this branch");
        }
    }

    private async Task<MenuGroupDto> MapToMenuGroupDtoAsync(FoodicsMenuGroup menuGroup)
    {
        var dto = ObjectMapper.Map<FoodicsMenuGroup, MenuGroupDto>(menuGroup);
        
        // Get active categories count
        dto.ActiveCategoriesCount = menuGroup.Categories.Count(c => c.IsActive);
        
        // Get last sync information
        var lastSyncRun = await (await _menuSyncRunRepository.GetQueryableAsync())
            .Where(sr => sr.MenuGroupId == menuGroup.Id && sr.IsCompleted)
            .OrderByDescending(sr => sr.StartedAt)
            .FirstOrDefaultAsync();

        if (lastSyncRun != null)
        {
            dto.LastSyncedAt = lastSyncRun.StartedAt;
            dto.LastSyncStatus = lastSyncRun.Status;
        }

        // Map categories
        dto.Categories = ObjectMapper.Map<List<MenuGroupCategory>, List<MenuGroupCategoryDto>>(
            menuGroup.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToList());

        return dto;
    }

    #endregion
}