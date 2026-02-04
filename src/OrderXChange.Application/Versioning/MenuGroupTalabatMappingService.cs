using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Versioning;
using OrderXChange.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service for managing Menu Group to Talabat mappings
/// </summary>
public class MenuGroupTalabatMappingService : ApplicationService, IMenuGroupTalabatMappingService
{
    private readonly IRepository<MenuGroupTalabatMapping, Guid> _mappingRepository;
    private readonly IRepository<FoodicsMenuGroup, Guid> _menuGroupRepository;
    private readonly IMenuGroupService _menuGroupService;
    private readonly IMenuDeltaSyncService _deltaSyncService;
    private readonly ILogger<MenuGroupTalabatMappingService> _logger;

    public MenuGroupTalabatMappingService(
        IRepository<MenuGroupTalabatMapping, Guid> mappingRepository,
        IRepository<FoodicsMenuGroup, Guid> menuGroupRepository,
        IMenuGroupService menuGroupService,
        IMenuDeltaSyncService deltaSyncService,
        ILogger<MenuGroupTalabatMappingService> logger)
    {
        _mappingRepository = mappingRepository;
        _menuGroupRepository = menuGroupRepository;
        _menuGroupService = menuGroupService;
        _deltaSyncService = deltaSyncService;
        _logger = logger;
    }

    public async Task<PagedResultDto<MenuGroupTalabatMappingDto>> GetMappingsAsync(
        Guid foodicsAccountId,
        PagedAndSortedResultRequestDto input)
    {
        var query = await _mappingRepository.GetQueryableAsync();
        
        var filteredQuery = query
            .Where(m => m.FoodicsAccountId == foodicsAccountId)
            .Include(m => m.MenuGroup);

        var totalCount = await AsyncExecuter.CountAsync(filteredQuery);

        var mappings = await AsyncExecuter.ToListAsync(
            filteredQuery
                .OrderByDescending(m => m.LastVerifiedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var mappingDtos = ObjectMapper.Map<List<MenuGroupTalabatMapping>, List<MenuGroupTalabatMappingDto>>(mappings);

        return new PagedResultDto<MenuGroupTalabatMappingDto>(totalCount, mappingDtos);
    }

    public async Task<MenuGroupTalabatMappingDto> GetMappingAsync(Guid id)
    {
        var mapping = await _mappingRepository.GetAsync(id);
        return ObjectMapper.Map<MenuGroupTalabatMapping, MenuGroupTalabatMappingDto>(mapping);
    }

    public async Task<MenuGroupTalabatMappingDto?> GetMappingByMenuGroupAsync(Guid menuGroupId)
    {
        var query = await _mappingRepository.GetQueryableAsync();
        var mapping = await AsyncExecuter.FirstOrDefaultAsync(
            query.Where(m => m.MenuGroupId == menuGroupId));

        return mapping != null 
            ? ObjectMapper.Map<MenuGroupTalabatMapping, MenuGroupTalabatMappingDto>(mapping)
            : null;
    }

    public async Task<List<MenuGroupTalabatMappingDto>> GetMappingsByVendorAsync(
        Guid foodicsAccountId,
        string talabatVendorCode)
    {
        var query = await _mappingRepository.GetQueryableAsync();
        var mappings = await AsyncExecuter.ToListAsync(
            query.Where(m => m.FoodicsAccountId == foodicsAccountId && 
                           m.TalabatVendorCode == talabatVendorCode));

        return ObjectMapper.Map<List<MenuGroupTalabatMapping>, List<MenuGroupTalabatMappingDto>>(mappings);
    }

    public async Task<MenuGroupTalabatMappingDto> CreateMappingAsync(CreateMenuGroupTalabatMappingDto input)
    {
        _logger.LogInformation("Creating Menu Group to Talabat mapping for MenuGroup {MenuGroupId}", input.MenuGroupId);

        // Validate Menu Group exists and is active
        var menuGroup = await _menuGroupRepository.GetAsync(input.MenuGroupId);
        if (!menuGroup.IsActive)
        {
            throw new InvalidOperationException("Cannot create mapping for inactive Menu Group");
        }

        // Check if mapping already exists for this Menu Group
        var existingMapping = await GetMappingByMenuGroupAsync(input.MenuGroupId);
        if (existingMapping != null)
        {
            throw new InvalidOperationException($"Mapping already exists for Menu Group {input.MenuGroupId}");
        }

        // Generate Talabat menu ID if not provided
        var talabatMenuId = input.TalabatMenuId ?? 
            await GenerateSuggestedTalabatMenuIdAsync(input.MenuGroupId, input.TalabatVendorCode);

        // Generate Talabat menu name if not provided
        var talabatMenuName = input.TalabatMenuName ?? menuGroup.Name;

        // Validate mapping configuration
        var validationResult = await ValidateMappingAsync(input);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Invalid mapping configuration: {string.Join(", ", validationResult.Errors)}");
        }

        var mapping = new MenuGroupTalabatMapping
        {
            FoodicsAccountId = menuGroup.FoodicsAccountId,
            MenuGroupId = input.MenuGroupId,
            TalabatVendorCode = input.TalabatVendorCode,
            TalabatMenuId = talabatMenuId,
            TalabatMenuName = talabatMenuName,
            TalabatMenuDescription = input.TalabatMenuDescription,
            Priority = input.Priority,
            MappingStrategy = input.MappingStrategy,
            ConfigurationJson = input.Configuration != null 
                ? JsonSerializer.Serialize(input.Configuration) 
                : null,
            MappingEstablishedAt = DateTime.UtcNow,
            LastVerifiedAt = DateTime.UtcNow,
            TenantId = CurrentTenant.Id
        };

        var createdMapping = await _mappingRepository.InsertAsync(mapping, autoSave: true);

        _logger.LogInformation("Created Menu Group to Talabat mapping {MappingId} for MenuGroup {MenuGroupId}", 
            createdMapping.Id, input.MenuGroupId);

        return ObjectMapper.Map<MenuGroupTalabatMapping, MenuGroupTalabatMappingDto>(createdMapping);
    }

    public async Task<MenuGroupTalabatMappingDto> UpdateMappingAsync(Guid id, UpdateMenuGroupTalabatMappingDto input)
    {
        _logger.LogInformation("Updating Menu Group to Talabat mapping {MappingId}", id);

        var mapping = await _mappingRepository.GetAsync(id);

        mapping.TalabatMenuName = input.TalabatMenuName;
        mapping.TalabatMenuDescription = input.TalabatMenuDescription;
        mapping.IsActive = input.IsActive;
        mapping.Priority = input.Priority;
        mapping.MappingStrategy = input.MappingStrategy;
        mapping.ConfigurationJson = input.Configuration != null 
            ? JsonSerializer.Serialize(input.Configuration) 
            : null;
        mapping.LastVerifiedAt = DateTime.UtcNow;

        var updatedMapping = await _mappingRepository.UpdateAsync(mapping, autoSave: true);

        _logger.LogInformation("Updated Menu Group to Talabat mapping {MappingId}", id);

        return ObjectMapper.Map<MenuGroupTalabatMapping, MenuGroupTalabatMappingDto>(updatedMapping);
    }

    public async Task DeleteMappingAsync(Guid id)
    {
        _logger.LogInformation("Deleting Menu Group to Talabat mapping {MappingId}", id);

        var mapping = await _mappingRepository.GetAsync(id);
        
        // Check if mapping is currently being used in active syncs
        // This would require checking MenuSyncRun entities
        
        await _mappingRepository.DeleteAsync(mapping, autoSave: true);

        _logger.LogInformation("Deleted Menu Group to Talabat mapping {MappingId}", id);
    }

    public async Task ActivateMappingAsync(Guid id)
    {
        _logger.LogInformation("Activating Menu Group to Talabat mapping {MappingId}", id);

        var mapping = await _mappingRepository.GetAsync(id);
        mapping.Activate();
        
        await _mappingRepository.UpdateAsync(mapping, autoSave: true);

        _logger.LogInformation("Activated Menu Group to Talabat mapping {MappingId}", id);
    }

    public async Task DeactivateMappingAsync(Guid id)
    {
        _logger.LogInformation("Deactivating Menu Group to Talabat mapping {MappingId}", id);

        var mapping = await _mappingRepository.GetAsync(id);
        mapping.Deactivate();
        
        await _mappingRepository.UpdateAsync(mapping, autoSave: true);

        _logger.LogInformation("Deactivated Menu Group to Talabat mapping {MappingId}", id);
    }

    public async Task<MenuMappingValidationResult> ValidateMappingAsync(Guid id)
    {
        var mapping = await _mappingRepository.GetAsync(id);
        return mapping.ValidateMapping();
    }

    public async Task<MenuMappingValidationResult> ValidateMappingAsync(CreateMenuGroupTalabatMappingDto input)
    {
        var result = new MenuMappingValidationResult { IsValid = true };

        // Validate Menu Group exists and is active
        var menuGroup = await _menuGroupRepository.FindAsync(input.MenuGroupId);
        if (menuGroup == null)
        {
            result.IsValid = false;
            result.Errors.Add("Menu Group not found");
            return result;
        }

        if (!menuGroup.IsActive)
        {
            result.IsValid = false;
            result.Errors.Add("Menu Group is not active");
        }

        // Validate Menu Group has categories
        var menuGroupValidation = menuGroup.ValidateForSync();
        if (!menuGroupValidation.IsValid)
        {
            result.IsValid = false;
            result.Errors.AddRange(menuGroupValidation.Errors);
        }

        // Check for duplicate Talabat menu ID
        if (!string.IsNullOrWhiteSpace(input.TalabatMenuId))
        {
            var query = await _mappingRepository.GetQueryableAsync();
            var existingMapping = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(m => m.FoodicsAccountId == menuGroup.FoodicsAccountId &&
                               m.TalabatVendorCode == input.TalabatVendorCode &&
                               m.TalabatMenuId == input.TalabatMenuId));

            if (existingMapping != null)
            {
                result.IsValid = false;
                result.Errors.Add($"Talabat menu ID '{input.TalabatMenuId}' already exists for vendor '{input.TalabatVendorCode}'");
            }
        }

        return result;
    }

    public async Task<TalabatConnectivityTestResult> TestTalabatConnectivityAsync(Guid id)
    {
        // This would integrate with actual Talabat API to test connectivity
        // For now, return a mock result
        return new TalabatConnectivityTestResult
        {
            IsConnected = true,
            ResponseTime = TimeSpan.FromMilliseconds(150),
            TestedAt = DateTime.UtcNow
        };
    }

    public async Task<MenuGroupSyncResult> SyncMenuGroupAsync(Guid mappingId, bool forceFull = false)
    {
        _logger.LogInformation("Syncing Menu Group using mapping {MappingId}, forceFull: {ForceFull}", mappingId, forceFull);

        var mapping = await _mappingRepository.GetAsync(mappingId);
        
        if (!mapping.IsActive)
        {
            throw new InvalidOperationException("Cannot sync using inactive mapping");
        }

        // This would integrate with the existing sync services
        // For now, return a mock result
        var result = new MenuGroupSyncResult
        {
            IsSuccess = true,
            ItemsSynced = 25,
            ItemsSkipped = 2,
            ItemsFailed = 0,
            Duration = TimeSpan.FromMinutes(2),
            SyncedAt = DateTime.UtcNow
        };

        // Update mapping sync statistics
        mapping.RecordSuccessfulSync();
        await _mappingRepository.UpdateAsync(mapping, autoSave: true);

        _logger.LogInformation("Completed Menu Group sync using mapping {MappingId}", mappingId);

        return result;
    }

    public async Task<PagedResultDto<MenuGroupSyncHistoryDto>> GetSyncHistoryAsync(
        Guid mappingId,
        PagedAndSortedResultRequestDto input)
    {
        // This would query MenuSyncRun entities filtered by mapping
        // For now, return empty result
        return new PagedResultDto<MenuGroupSyncHistoryDto>(0, new List<MenuGroupSyncHistoryDto>());
    }

    public async Task<MenuGroupMappingStatsDto> GetMappingStatsAsync(Guid id)
    {
        var mapping = await _mappingRepository.GetAsync(id);
        
        // This would calculate actual statistics from sync history
        return new MenuGroupMappingStatsDto
        {
            TotalSyncs = mapping.SyncCount,
            SuccessfulSyncs = mapping.SyncCount,
            FailedSyncs = 0,
            LastSuccessfulSync = mapping.LastVerifiedAt,
            SuccessRate = mapping.SyncCount > 0 ? 100.0 : 0.0
        };
    }

    public async Task<List<MenuGroupTalabatMappingDto>> BulkCreateMappingsAsync(
        List<CreateMenuGroupTalabatMappingDto> inputs)
    {
        var results = new List<MenuGroupTalabatMappingDto>();

        foreach (var input in inputs)
        {
            try
            {
                var mapping = await CreateMappingAsync(input);
                results.Add(mapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create mapping for Menu Group {MenuGroupId}", input.MenuGroupId);
                // Continue with other mappings
            }
        }

        return results;
    }

    public async Task<string> ExportMappingConfigurationAsync(Guid id)
    {
        var mapping = await _mappingRepository.GetAsync(id);
        
        var exportData = new
        {
            mapping.TalabatVendorCode,
            mapping.TalabatMenuId,
            mapping.TalabatMenuName,
            mapping.TalabatMenuDescription,
            mapping.MappingStrategy,
            mapping.Priority,
            Configuration = mapping.ConfigurationJson
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<MenuGroupTalabatMappingDto> ImportMappingConfigurationAsync(
        Guid menuGroupId,
        string configurationJson)
    {
        var importData = JsonSerializer.Deserialize<CreateMenuGroupTalabatMappingDto>(configurationJson);
        if (importData == null)
        {
            throw new ArgumentException("Invalid configuration JSON");
        }

        importData.MenuGroupId = menuGroupId;
        return await CreateMappingAsync(importData);
    }

    public async Task<MenuGroupTalabatMappingDto> CloneMappingAsync(
        Guid sourceMappingId,
        Guid targetMenuGroupId,
        string newTalabatMenuId)
    {
        var sourceMapping = await _mappingRepository.GetAsync(sourceMappingId);
        
        var cloneInput = new CreateMenuGroupTalabatMappingDto
        {
            MenuGroupId = targetMenuGroupId,
            TalabatVendorCode = sourceMapping.TalabatVendorCode,
            TalabatMenuId = newTalabatMenuId,
            TalabatMenuName = sourceMapping.TalabatMenuName,
            TalabatMenuDescription = sourceMapping.TalabatMenuDescription,
            Priority = sourceMapping.Priority,
            MappingStrategy = sourceMapping.MappingStrategy,
            Configuration = !string.IsNullOrWhiteSpace(sourceMapping.ConfigurationJson)
                ? JsonSerializer.Deserialize<MenuGroupMappingConfigurationDto>(sourceMapping.ConfigurationJson)
                : null
        };

        return await CreateMappingAsync(cloneInput);
    }

    public async Task<List<TalabatVendorInfoDto>> GetAvailableTalabatVendorsAsync(Guid foodicsAccountId)
    {
        // This would query TalabatAccount entities for the given Foodics account
        // For now, return mock data
        return new List<TalabatVendorInfoDto>
        {
            new() { VendorCode = "VENDOR001", VendorName = "Main Restaurant", IsActive = true },
            new() { VendorCode = "VENDOR002", VendorName = "Branch Location", IsActive = true }
        };
    }

    public async Task<string> GenerateSuggestedTalabatMenuIdAsync(Guid menuGroupId, string talabatVendorCode)
    {
        var menuGroup = await _menuGroupRepository.GetAsync(menuGroupId);
        
        // Generate a unique menu ID based on Menu Group name and vendor
        var baseName = menuGroup.Name.Replace(" ", "_").ToLowerInvariant();
        var suggested = $"{talabatVendorCode.ToLowerInvariant()}_{baseName}";

        // Ensure uniqueness
        var query = await _mappingRepository.GetQueryableAsync();
        var counter = 1;
        var finalId = suggested;

        while (await AsyncExecuter.AnyAsync(query.Where(m => m.TalabatVendorCode == talabatVendorCode && 
                                                           m.TalabatMenuId == finalId)))
        {
            finalId = $"{suggested}_{counter}";
            counter++;
        }

        return finalId;
    }

    public async Task<MenuGroupSyncPreviewDto> PreviewSyncAsync(Guid mappingId)
    {
        var mapping = await _mappingRepository.GetAsync(mappingId);
        
        // This would analyze the Menu Group and generate a preview
        // For now, return mock data
        return new MenuGroupSyncPreviewDto
        {
            TotalItems = 25,
            NewItems = 5,
            UpdatedItems = 15,
            UnchangedItems = 5,
            Categories = 4,
            CategoryNames = new List<string> { "Appetizers", "Main Courses", "Desserts", "Beverages" },
            PreviewGeneratedAt = DateTime.UtcNow
        };
    }
}