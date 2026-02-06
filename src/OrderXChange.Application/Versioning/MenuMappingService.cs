using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Implementation of menu mapping service
/// Manages stable ID-based mappings between Foodics and Talabat
/// </summary>
public class MenuMappingService : IMenuMappingService, ITransientDependency
{
    private readonly IRepository<MenuItemMapping, Guid> _mappingRepository;
    private readonly ILogger<MenuMappingService> _logger;
    private readonly Volo.Abp.Guids.IGuidGenerator _guidGenerator;

    public MenuMappingService(
        IRepository<MenuItemMapping, Guid> mappingRepository,
        ILogger<MenuMappingService> logger,
        Volo.Abp.Guids.IGuidGenerator guidGenerator)
    {
        _mappingRepository = mappingRepository;
        _logger = logger;
        _guidGenerator = guidGenerator;
    }

    public async Task<MenuItemMapping> GetOrCreateMappingAsync(
        Guid foodicsAccountId,
        string? branchId,
        string entityType,
        string foodicsId,
        string? currentName = null,
        string? parentFoodicsId = null,
        CancellationToken cancellationToken = default)
    {
        // Try to find existing mapping
        var existingMapping = await GetMappingByFoodicsIdAsync(
            foodicsAccountId, branchId, entityType, foodicsId, cancellationToken);

        if (existingMapping != null)
        {
            // Update name if provided and different
            if (!string.IsNullOrEmpty(currentName) && existingMapping.CurrentFoodicsName != currentName)
            {
                existingMapping.UpdateNames(currentName);
                await _mappingRepository.UpdateAsync(existingMapping, autoSave: true, cancellationToken: cancellationToken);
            }

            existingMapping.RecordSuccessfulSync();
            return existingMapping;
        }

        // Create new mapping
        var talabatRemoteCode = MenuMappingStrategy.GenerateTalabatRemoteCode(entityType, foodicsId);

        // Find parent mapping if specified
        Guid? parentMappingId = null;
        if (!string.IsNullOrEmpty(parentFoodicsId))
        {
            var parentEntityType = GetParentEntityType(entityType);
            if (!string.IsNullOrEmpty(parentEntityType))
            {
                var parentMapping = await GetMappingByFoodicsIdAsync(
                    foodicsAccountId, branchId, parentEntityType, parentFoodicsId, cancellationToken);
                parentMappingId = parentMapping?.Id;
            }
        }

        var newMapping = new MenuItemMapping
        {
            Id = _guidGenerator.Create(),
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            EntityType = entityType,
            FoodicsId = foodicsId,
            TalabatRemoteCode = talabatRemoteCode,
            CurrentFoodicsName = currentName,
            ParentMappingId = parentMappingId,
            FirstSyncedAt = DateTime.UtcNow,
            LastVerifiedAt = DateTime.UtcNow,
            SyncCount = 1
        };

        await _mappingRepository.InsertAsync(newMapping, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created new mapping. FoodicsId={FoodicsId}, TalabatRemoteCode={TalabatRemoteCode}, EntityType={EntityType}",
            foodicsId, talabatRemoteCode, entityType);

        return newMapping;
    }

    public async Task<MenuItemMapping?> GetMappingByFoodicsIdAsync(
        Guid foodicsAccountId,
        string? branchId,
        string entityType,
        string foodicsId,
        CancellationToken cancellationToken = default)
    {
        return await _mappingRepository.FirstOrDefaultAsync(
            m => m.FoodicsAccountId == foodicsAccountId &&
                 m.BranchId == branchId &&
                 m.EntityType == entityType &&
                 m.FoodicsId == foodicsId &&
                 m.IsActive,
            cancellationToken: cancellationToken);
    }

    public async Task<MenuItemMapping?> GetMappingByTalabatRemoteCodeAsync(
        Guid foodicsAccountId,
        string? branchId,
        string talabatRemoteCode,
        CancellationToken cancellationToken = default)
    {
        return await _mappingRepository.FirstOrDefaultAsync(
            m => m.FoodicsAccountId == foodicsAccountId &&
                 m.BranchId == branchId &&
                 m.TalabatRemoteCode == talabatRemoteCode &&
                 m.IsActive,
            cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, MenuItemMapping>> BulkCreateOrUpdateMappingsAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> products,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Bulk creating/updating mappings for {ProductCount} products. AccountId={AccountId}, BranchId={BranchId}",
            products.Count, foodicsAccountId, branchId ?? "ALL");

        var result = new Dictionary<string, MenuItemMapping>();

        // Get all existing mappings for this account/branch
        var existingMappings = await GetActiveMappingsAsync(foodicsAccountId, branchId, cancellationToken: cancellationToken);
        var existingMappingDict = existingMappings.ToDictionary(m => $"{m.EntityType}:{m.FoodicsId}", m => m);

        var newMappings = new List<MenuItemMapping>();
        var updatedMappings = new List<MenuItemMapping>();

        // Process products
        foreach (var product in products)
        {
            var productMapping = await ProcessEntityMapping(
                foodicsAccountId, branchId, MenuMappingEntityType.Product, product.Id, product.Name,
                existingMappingDict, newMappings, updatedMappings);
            result[product.Id] = productMapping;

            // Process category
            if (product.Category != null && !string.IsNullOrEmpty(product.Category.Id))
            {
                var categoryMapping = await ProcessEntityMapping(
                    foodicsAccountId, branchId, MenuMappingEntityType.Category, product.Category.Id, product.Category.Name,
                    existingMappingDict, newMappings, updatedMappings);
                result[product.Category.Id] = categoryMapping;
            }

            // Process modifiers and options
            if (product.Modifiers != null)
            {
                foreach (var modifier in product.Modifiers)
                {
                    if (string.IsNullOrEmpty(modifier.Id)) continue;

                    var modifierMapping = await ProcessEntityMapping(
                        foodicsAccountId, branchId, MenuMappingEntityType.Modifier, modifier.Id, modifier.Name,
                        existingMappingDict, newMappings, updatedMappings);
                    result[modifier.Id] = modifierMapping;

                    // Process modifier options
                    if (modifier.Options != null)
                    {
                        foreach (var option in modifier.Options)
                        {
                            if (string.IsNullOrEmpty(option.Id)) continue;

                            var optionMapping = await ProcessEntityMapping(
                                foodicsAccountId, branchId, MenuMappingEntityType.ModifierOption, option.Id, option.Name,
                                existingMappingDict, newMappings, updatedMappings, modifier.Id);
                            result[option.Id] = optionMapping;
                        }
                    }
                }
            }
        }

        // Bulk insert new mappings
        if (newMappings.Any())
        {
            await _mappingRepository.InsertManyAsync(newMappings, autoSave: false, cancellationToken: cancellationToken);
        }

        // Bulk update existing mappings
        if (updatedMappings.Any())
        {
            await _mappingRepository.UpdateManyAsync(updatedMappings, autoSave: false, cancellationToken: cancellationToken);
        }

        // Save all changes
        if (newMappings.Any() || updatedMappings.Any())
        {
            await _mappingRepository.GetDbContext().SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Bulk mapping completed. Created={Created}, Updated={Updated}, Total={Total}",
            newMappings.Count, updatedMappings.Count, result.Count);

        return result;
    }

    public async Task<int> UpdateTalabatInternalIdsAsync(
        Dictionary<string, string> mappingUpdates,
        CancellationToken cancellationToken = default)
    {
        if (!mappingUpdates.Any())
            return 0;

        var remoteCodesList = mappingUpdates.Keys.ToList();
        var mappings = await _mappingRepository.GetListAsync(
            m => remoteCodesList.Contains(m.TalabatRemoteCode) && m.IsActive,
            cancellationToken: cancellationToken);

        var updatedCount = 0;
        foreach (var mapping in mappings)
        {
            if (mappingUpdates.TryGetValue(mapping.TalabatRemoteCode, out var talabatInternalId))
            {
                mapping.SetTalabatInternalId(talabatInternalId);
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await _mappingRepository.UpdateManyAsync(mappings, autoSave: true, cancellationToken: cancellationToken);
            
            _logger.LogInformation(
                "Updated Talabat internal IDs for {Count} mappings",
                updatedCount);
        }

        return updatedCount;
    }

    public async Task<int> DeactivateObsoleteMappingsAsync(
        Guid foodicsAccountId,
        string? branchId,
        HashSet<string> currentFoodicsIds,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        var existingMappings = await _mappingRepository.GetListAsync(
            m => m.FoodicsAccountId == foodicsAccountId &&
                 m.BranchId == branchId &&
                 m.EntityType == entityType &&
                 m.IsActive,
            cancellationToken: cancellationToken);

        var obsoleteMappings = existingMappings
            .Where(m => !currentFoodicsIds.Contains(m.FoodicsId))
            .ToList();

        if (obsoleteMappings.Any())
        {
            foreach (var mapping in obsoleteMappings)
            {
                mapping.Deactivate();
            }

            await _mappingRepository.UpdateManyAsync(obsoleteMappings, autoSave: true, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Deactivated {Count} obsolete {EntityType} mappings",
                obsoleteMappings.Count, entityType);
        }

        return obsoleteMappings.Count;
    }

    public async Task<List<MenuItemMapping>> GetActiveMappingsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        string? entityType = null,
        CancellationToken cancellationToken = default)
    {
        var query = await _mappingRepository.GetQueryableAsync();

        return await query
            .Where(m => m.FoodicsAccountId == foodicsAccountId)
            .Where(m => branchId == null || m.BranchId == branchId)
            .Where(m => entityType == null || m.EntityType == entityType)
            .Where(m => m.IsActive)
            .OrderBy(m => m.EntityType)
            .ThenBy(m => m.CreationTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<MappingValidationResult> ValidateAndFixMappingsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new MappingValidationResult { IsValid = true };

        var mappings = await GetActiveMappingsAsync(foodicsAccountId, branchId, cancellationToken: cancellationToken);
        result.TotalMappings = mappings.Count;

        var fixedMappings = new List<MenuItemMapping>();

        // Check for duplicate Talabat remote codes
        var duplicateRemoteCodes = mappings
            .GroupBy(m => m.TalabatRemoteCode)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var duplicateGroup in duplicateRemoteCodes)
        {
            result.IsValid = false;
            result.Issues.Add($"Duplicate Talabat remote code: {duplicateGroup.Key}");

            // Keep the most recent mapping, deactivate others
            var orderedDuplicates = duplicateGroup.OrderByDescending(m => m.LastVerifiedAt).ToList();
            for (int i = 1; i < orderedDuplicates.Count; i++)
            {
                orderedDuplicates[i].Deactivate();
                fixedMappings.Add(orderedDuplicates[i]);
                result.FixesApplied.Add($"Deactivated duplicate mapping: {orderedDuplicates[i].Id}");
            }
        }

        // Check for orphaned child mappings
        var parentMappingIds = mappings.Where(m => m.ParentMappingId.HasValue).Select(m => m.ParentMappingId!.Value).Distinct().ToList();
        var existingParentIds = mappings.Select(m => m.Id).ToHashSet();
        var orphanedMappings = mappings.Where(m => m.ParentMappingId.HasValue && !existingParentIds.Contains(m.ParentMappingId!.Value)).ToList();

        foreach (var orphanedMapping in orphanedMappings)
        {
            result.Issues.Add($"Orphaned mapping: {orphanedMapping.Id} (parent not found)");
            orphanedMapping.ParentMappingId = null;
            fixedMappings.Add(orphanedMapping);
            result.FixesApplied.Add($"Removed orphaned parent reference: {orphanedMapping.Id}");
        }

        // Save fixes
        if (fixedMappings.Any())
        {
            await _mappingRepository.UpdateManyAsync(fixedMappings, autoSave: true, cancellationToken: cancellationToken);
            result.FixedMappings = fixedMappings.Count;
        }

        result.ValidMappings = result.TotalMappings - result.FixedMappings;

        _logger.LogInformation(
            "Mapping validation completed. Total={Total}, Valid={Valid}, Fixed={Fixed}",
            result.TotalMappings, result.ValidMappings, result.FixedMappings);

        return result;
    }

    public async Task<int> CleanupOldMappingsAsync(
        int retentionDays = 90,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        var oldMappings = await _mappingRepository.GetListAsync(
            m => !m.IsActive && m.LastModificationTime < cutoffDate,
            cancellationToken: cancellationToken);

        if (oldMappings.Any())
        {
            await _mappingRepository.DeleteManyAsync(oldMappings, autoSave: true, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Cleaned up {Count} old inactive mappings older than {Days} days",
                oldMappings.Count, retentionDays);
        }

        return oldMappings.Count;
    }

    public async Task<MappingStatistics> GetMappingStatisticsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var mappings = await _mappingRepository.GetListAsync(
            m => m.FoodicsAccountId == foodicsAccountId &&
                 (branchId == null || m.BranchId == branchId),
            cancellationToken: cancellationToken);

        var activeMappings = mappings.Where(m => m.IsActive).ToList();

        var statistics = new MappingStatistics
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            TotalMappings = mappings.Count,
            ActiveMappings = activeMappings.Count,
            InactiveMappings = mappings.Count - activeMappings.Count,
            MappingsByEntityType = activeMappings.GroupBy(m => m.EntityType).ToDictionary(g => g.Key, g => g.Count()),
            MappingsWithTalabatIds = activeMappings.Count(m => !string.IsNullOrEmpty(m.TalabatInternalId)),
            OrphanedMappings = activeMappings.Count(m => m.ParentMappingId.HasValue && 
                !activeMappings.Any(am => am.Id == m.ParentMappingId))
        };

        if (mappings.Any())
        {
            statistics.OldestMapping = mappings.Min(m => m.CreationTime);
            statistics.NewestMapping = mappings.Max(m => m.CreationTime);
            statistics.AverageSyncCount = mappings.Average(m => m.SyncCount);
        }

        return statistics;
    }

    #region Private Methods

    private async Task<MenuItemMapping> ProcessEntityMapping(
        Guid foodicsAccountId,
        string? branchId,
        string entityType,
        string foodicsId,
        string? currentName,
        Dictionary<string, MenuItemMapping> existingMappingDict,
        List<MenuItemMapping> newMappings,
        List<MenuItemMapping> updatedMappings,
        string? parentFoodicsId = null)
    {
        var key = $"{entityType}:{foodicsId}";

        if (existingMappingDict.TryGetValue(key, out var existingMapping))
        {
            // Update existing mapping
            if (!string.IsNullOrEmpty(currentName) && existingMapping.CurrentFoodicsName != currentName)
            {
                existingMapping.UpdateNames(currentName);
                updatedMappings.Add(existingMapping);
            }

            existingMapping.RecordSuccessfulSync();
            return existingMapping;
        }

        // Create new mapping
        var talabatRemoteCode = MenuMappingStrategy.GenerateTalabatRemoteCode(entityType, foodicsId);

        // Find parent mapping if specified
        Guid? parentMappingId = null;
        if (!string.IsNullOrEmpty(parentFoodicsId))
        {
            var parentEntityType = GetParentEntityType(entityType);
            if (!string.IsNullOrEmpty(parentEntityType))
            {
                var parentKey = $"{parentEntityType}:{parentFoodicsId}";
                if (existingMappingDict.TryGetValue(parentKey, out var parentMapping))
                {
                    parentMappingId = parentMapping.Id;
                }
            }
        }

        var newMapping = new MenuItemMapping
        {
            Id = _guidGenerator.Create(),
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            EntityType = entityType,
            FoodicsId = foodicsId,
            TalabatRemoteCode = talabatRemoteCode,
            CurrentFoodicsName = currentName,
            ParentMappingId = parentMappingId,
            FirstSyncedAt = DateTime.UtcNow,
            LastVerifiedAt = DateTime.UtcNow,
            SyncCount = 1
        };

        newMappings.Add(newMapping);
        existingMappingDict[key] = newMapping; // Add to dict for parent lookups

        return newMapping;
    }

    private static string? GetParentEntityType(string entityType)
    {
        return entityType switch
        {
            MenuMappingEntityType.Product => MenuMappingEntityType.Category,
            MenuMappingEntityType.ModifierOption => MenuMappingEntityType.Modifier,
            _ => null
        };
    }

    private static string CalculateStructureHash(object entity)
    {
        var json = JsonSerializer.Serialize(entity);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    #endregion
}
