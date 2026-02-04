using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
/// Service for managing modifier lifecycle including tracking, versioning, and price changes
/// Handles safe price updates and maintains comprehensive audit trails
/// </summary>
public class ModifierLifecycleService : IModifierLifecycleService, ITransientDependency
{
    private readonly IRepository<ModifierGroup, Guid> _modifierGroupRepository;
    private readonly IRepository<ModifierOption, Guid> _modifierOptionRepository;
    private readonly IRepository<ModifierOptionPriceHistory, Guid> _priceHistoryRepository;
    private readonly IRepository<ModifierGroupVersion, Guid> _groupVersionRepository;
    private readonly IRepository<ModifierOptionVersion, Guid> _optionVersionRepository;
    private readonly IRepository<ProductModifierAssignment, Guid> _assignmentRepository;
    private readonly ILogger<ModifierLifecycleService> _logger;

    public ModifierLifecycleService(
        IRepository<ModifierGroup, Guid> modifierGroupRepository,
        IRepository<ModifierOption, Guid> modifierOptionRepository,
        IRepository<ModifierOptionPriceHistory, Guid> priceHistoryRepository,
        IRepository<ModifierGroupVersion, Guid> groupVersionRepository,
        IRepository<ModifierOptionVersion, Guid> optionVersionRepository,
        IRepository<ProductModifierAssignment, Guid> assignmentRepository,
        ILogger<ModifierLifecycleService> logger)
    {
        _modifierGroupRepository = modifierGroupRepository;
        _modifierOptionRepository = modifierOptionRepository;
        _priceHistoryRepository = priceHistoryRepository;
        _groupVersionRepository = groupVersionRepository;
        _optionVersionRepository = optionVersionRepository;
        _assignmentRepository = assignmentRepository;
        _logger = logger;
    }

    public async Task<ModifierSyncResult> SyncModifiersAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId,
        List<FoodicsProductDetailDto> products,
        CancellationToken cancellationToken = default)
    {
        var result = new ModifierSyncResult
        {
            StartedAt = DateTime.UtcNow,
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            MenuGroupId = menuGroupId
        };

        try
        {
            _logger.LogInformation(
                "Starting modifier sync. Account={AccountId}, Branch={BranchId}, MenuGroup={MenuGroupId}, Products={ProductCount}",
                foodicsAccountId, branchId ?? "ALL", menuGroupId?.ToString() ?? "ALL", products.Count);

            // Extract all modifiers from products
            var allModifiers = ExtractModifiersFromProducts(products);
            result.TotalModifiersFound = allModifiers.Count;

            // Get existing modifier groups
            var existingGroups = await GetModifierGroupsAsync(foodicsAccountId, branchId, menuGroupId, false, cancellationToken);
            var existingGroupsDict = existingGroups.ToDictionary(g => g.FoodicsModifierGroupId, g => g);

            // Sync modifier groups
            foreach (var modifierData in allModifiers)
            {
                try
                {
                    var syncedGroup = await SyncModifierGroupAsync(
                        foodicsAccountId, branchId, menuGroupId, modifierData, existingGroupsDict, cancellationToken);
                    
                    if (syncedGroup.WasCreated)
                        result.ModifierGroupsCreated++;
                    else if (syncedGroup.WasUpdated)
                        result.ModifierGroupsUpdated++;

                    result.ModifierOptionsCreated += syncedGroup.OptionsCreated;
                    result.ModifierOptionsUpdated += syncedGroup.OptionsUpdated;
                    result.PriceChanges += syncedGroup.PriceChanges;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync modifier group {ModifierId}", modifierData.Id);
                    result.Errors.Add($"Failed to sync modifier {modifierData.Id}: {ex.Message}");
                }
            }

            // Update product-modifier assignments
            await UpdateProductModifierAssignmentsAsync(foodicsAccountId, branchId, menuGroupId, products, cancellationToken);

            result.Success = !result.Errors.Any();
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            _logger.LogInformation(
                "Modifier sync completed. Success={Success}, Groups Created={Created}, Groups Updated={Updated}, Duration={Duration}ms",
                result.Success, result.ModifierGroupsCreated, result.ModifierGroupsUpdated, result.Duration?.TotalMilliseconds ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Modifier sync failed");
            result.Success = false;
            result.Errors.Add($"Sync failed: {ex.Message}");
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;
            return result;
        }
    }

    public async Task<ModifierGroup> UpdateModifierGroupAsync(
        Guid modifierGroupId,
        string name,
        string? nameLocalized,
        int? minSelection,
        int? maxSelection,
        bool isRequired,
        string? changeReason = null,
        CancellationToken cancellationToken = default)
    {
        var modifierGroup = await _modifierGroupRepository.GetAsync(modifierGroupId, cancellationToken: cancellationToken);
        
        var structureHash = CalculateModifierGroupHash(name, minSelection, maxSelection, isRequired);
        modifierGroup.UpdateStructure(name, nameLocalized, minSelection, maxSelection, isRequired, structureHash);

        await _modifierGroupRepository.UpdateAsync(modifierGroup, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Updated modifier group {ModifierGroupId} to version {Version}. Reason: {Reason}",
            modifierGroupId, modifierGroup.Version, changeReason ?? "Not specified");

        return modifierGroup;
    }

    public async Task<ModifierOption> UpdateModifierOptionPriceAsync(
        Guid modifierOptionId,
        decimal newPrice,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var modifierOption = await _modifierOptionRepository.GetAsync(modifierOptionId, cancellationToken: cancellationToken);
        
        var oldPrice = modifierOption.Price;
        modifierOption.UpdatePrice(newPrice, reason);

        await _modifierOptionRepository.UpdateAsync(modifierOption, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Updated modifier option {OptionId} price from {OldPrice} to {NewPrice}. Reason: {Reason}",
            modifierOptionId, oldPrice, newPrice, reason ?? "Not specified");

        return modifierOption;
    }

    public async Task<List<ModifierGroup>> GetModifierGroupsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        Guid? menuGroupId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = await _modifierGroupRepository.GetQueryableAsync();
        
        query = query.Where(mg => mg.FoodicsAccountId == foodicsAccountId);
        
        if (branchId != null)
            query = query.Where(mg => mg.BranchId == branchId);
        
        if (menuGroupId.HasValue)
            query = query.Where(mg => mg.MenuGroupId == menuGroupId);
        
        if (activeOnly)
            query = query.Where(mg => mg.IsActive);

        return await query
            .Include(mg => mg.Options.Where(o => !activeOnly || o.IsActive))
            .OrderBy(mg => mg.SortOrder)
            .ThenBy(mg => mg.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ModifierOption>> GetModifierOptionsAsync(
        Guid modifierGroupId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = await _modifierOptionRepository.GetQueryableAsync();
        
        query = query.Where(mo => mo.ModifierGroupId == modifierGroupId);
        
        if (activeOnly)
            query = query.Where(mo => mo.IsActive);

        return await query
            .OrderBy(mo => mo.SortOrder)
            .ThenBy(mo => mo.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ModifierOptionPriceHistory>> GetPriceHistoryAsync(
        Guid modifierOptionId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = await _priceHistoryRepository.GetQueryableAsync();
        
        query = query.Where(ph => ph.ModifierOptionId == modifierOptionId);
        
        if (fromDate.HasValue)
            query = query.Where(ph => ph.ChangedAt >= fromDate.Value);
        
        if (toDate.HasValue)
            query = query.Where(ph => ph.ChangedAt <= toDate.Value);

        return await query
            .OrderByDescending(ph => ph.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ModifierChangeDetectionResult> DetectModifierChangesAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId,
        List<FoodicsProductDetailDto> currentProducts,
        CancellationToken cancellationToken = default)
    {
        var result = new ModifierChangeDetectionResult
        {
            DetectedAt = DateTime.UtcNow,
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            MenuGroupId = menuGroupId
        };

        // Get current modifiers from Foodics
        var currentModifiers = ExtractModifiersFromProducts(currentProducts);
        var currentModifiersDict = currentModifiers.ToDictionary(m => m.Id, m => m);

        // Get existing modifier groups
        var existingGroups = await GetModifierGroupsAsync(foodicsAccountId, branchId, menuGroupId, false, cancellationToken);
        var existingGroupsDict = existingGroups.ToDictionary(g => g.FoodicsModifierGroupId, g => g);

        // Detect changes
        foreach (var currentModifier in currentModifiers)
        {
            if (existingGroupsDict.TryGetValue(currentModifier.Id, out var existingGroup))
            {
                // Check for structure changes
                var currentHash = CalculateModifierGroupHash(
                    currentModifier.Name ?? "", 
                    currentModifier.MinAllowed, 
                    currentModifier.MaxAllowed, 
                    false); // IsRequired not available in DTO

                if (existingGroup.StructureHash != currentHash)
                {
                    result.ModifiedGroups.Add(new ModifierGroupChange
                    {
                        ModifierGroupId = existingGroup.Id,
                        FoodicsModifierGroupId = currentModifier.Id,
                        ChangeType = "Structure",
                        OldValue = existingGroup.StructureHash,
                        NewValue = currentHash
                    });
                }

                // Check for option changes
                await DetectOptionChangesAsync(existingGroup, currentModifier, result, cancellationToken);
            }
            else
            {
                // New modifier group
                result.NewGroups.Add(currentModifier.Id);
            }
        }

        // Detect removed modifier groups
        foreach (var existingGroup in existingGroups)
        {
            if (!currentModifiersDict.ContainsKey(existingGroup.FoodicsModifierGroupId))
            {
                result.RemovedGroups.Add(existingGroup.FoodicsModifierGroupId);
            }
        }

        result.HasChanges = result.NewGroups.Any() || result.RemovedGroups.Any() || result.ModifiedGroups.Any() || result.PriceChanges.Any();

        return result;
    }

    public async Task<List<ModifierGroup>> GetModifierGroupsNeedingSyncAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        Guid? menuGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var query = await _modifierGroupRepository.GetQueryableAsync();
        
        query = query.Where(mg => mg.FoodicsAccountId == foodicsAccountId && mg.IsActive && !mg.IsSyncedToTalabat);
        
        if (branchId != null)
            query = query.Where(mg => mg.BranchId == branchId);
        
        if (menuGroupId.HasValue)
            query = query.Where(mg => mg.MenuGroupId == menuGroupId);

        return await query
            .Include(mg => mg.Options.Where(o => o.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task MarkModifierGroupsAsSyncedAsync(
        List<Guid> modifierGroupIds,
        string talabatVendorCode,
        CancellationToken cancellationToken = default)
    {
        var modifierGroups = await _modifierGroupRepository.GetListAsync(
            mg => modifierGroupIds.Contains(mg.Id), cancellationToken: cancellationToken);

        foreach (var group in modifierGroups)
        {
            group.MarkAsSynced(talabatVendorCode);
            
            // Also mark options as synced
            foreach (var option in group.Options)
            {
                option.MarkAsSynced();
            }
        }

        await _modifierGroupRepository.UpdateManyAsync(modifierGroups, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Marked {Count} modifier groups as synced to Talabat vendor {VendorCode}",
            modifierGroups.Count, talabatVendorCode);
    }

    public async Task<ModifierValidationResult> ValidateModifiersForTalabatAsync(
        List<ModifierGroup> modifierGroups,
        CancellationToken cancellationToken = default)
    {
        var result = new ModifierValidationResult { IsValid = true };

        foreach (var group in modifierGroups)
        {
            var groupValidation = group.ValidateConfiguration();
            if (!groupValidation.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(groupValidation.Errors.Select(e => $"Group '{group.Name}': {e}"));
            }
            result.Warnings.AddRange(groupValidation.Warnings.Select(w => $"Group '{group.Name}': {w}"));

            // Validate options
            foreach (var option in group.Options.Where(o => o.IsActive))
            {
                if (string.IsNullOrWhiteSpace(option.Name))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Option in group '{group.Name}' has no name");
                }

                if (option.Price < 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Option '{option.Name}' in group '{group.Name}' has negative price");
                }
            }
        }

        return result;
    }

    public async Task<ModifierAnalyticsResult> GetModifierAnalyticsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        Guid? menuGroupId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ModifierAnalyticsResult
        {
            GeneratedAt = DateTime.UtcNow,
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            MenuGroupId = menuGroupId,
            FromDate = fromDate,
            ToDate = toDate
        };

        // Get modifier groups
        var modifierGroups = await GetModifierGroupsAsync(foodicsAccountId, branchId, menuGroupId, false, cancellationToken);
        result.TotalModifierGroups = modifierGroups.Count;
        result.ActiveModifierGroups = modifierGroups.Count(mg => mg.IsActive);

        // Get total options
        result.TotalModifierOptions = modifierGroups.Sum(mg => mg.Options.Count);
        result.ActiveModifierOptions = modifierGroups.Sum(mg => mg.Options.Count(o => o.IsActive));

        // Get price change analytics
        var priceHistoryQuery = await _priceHistoryRepository.GetQueryableAsync();
        
        if (fromDate.HasValue)
            priceHistoryQuery = priceHistoryQuery.Where(ph => ph.ChangedAt >= fromDate.Value);
        
        if (toDate.HasValue)
            priceHistoryQuery = priceHistoryQuery.Where(ph => ph.ChangedAt <= toDate.Value);

        var priceChanges = await priceHistoryQuery
            .Where(ph => modifierGroups.SelectMany(mg => mg.Options).Select(o => o.Id).Contains(ph.ModifierOptionId))
            .ToListAsync(cancellationToken);

        result.TotalPriceChanges = priceChanges.Count;
        result.PriceIncreases = priceChanges.Count(pc => pc.ChangeAmount > 0);
        result.PriceDecreases = priceChanges.Count(pc => pc.ChangeAmount < 0);
        result.AveragePriceChange = priceChanges.Any() ? priceChanges.Average(pc => pc.ChangePercentage) : 0;

        return result;
    }

    public async Task<ModifierGroup> RollbackModifierGroupAsync(
        Guid modifierGroupId,
        int targetVersion,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var modifierGroup = await _modifierGroupRepository.GetAsync(modifierGroupId, cancellationToken: cancellationToken);
        
        var targetVersionSnapshot = await _groupVersionRepository.FirstOrDefaultAsync(
            v => v.ModifierGroupId == modifierGroupId && v.Version == targetVersion,
            cancellationToken: cancellationToken);

        if (targetVersionSnapshot == null)
        {
            throw new InvalidOperationException($"Version {targetVersion} not found for modifier group {modifierGroupId}");
        }

        if (!targetVersionSnapshot.CanRollback())
        {
            throw new InvalidOperationException($"Cannot rollback to version {targetVersion} - too old or restricted");
        }

        // Create current version snapshot before rollback
        modifierGroup.UpdateStructure(
            targetVersionSnapshot.Name,
            targetVersionSnapshot.NameLocalized,
            targetVersionSnapshot.MinSelection,
            targetVersionSnapshot.MaxSelection,
            targetVersionSnapshot.IsRequired,
            targetVersionSnapshot.StructureHash);

        await _modifierGroupRepository.UpdateAsync(modifierGroup, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogWarning(
            "Rolled back modifier group {ModifierGroupId} from version {CurrentVersion} to version {TargetVersion}. Reason: {Reason}",
            modifierGroupId, modifierGroup.Version, targetVersion, reason ?? "Not specified");

        return modifierGroup;
    }

    public async Task<ModifierOption> RollbackModifierOptionAsync(
        Guid modifierOptionId,
        int targetVersion,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var modifierOption = await _modifierOptionRepository.GetAsync(modifierOptionId, cancellationToken: cancellationToken);
        
        var targetVersionSnapshot = await _optionVersionRepository.FirstOrDefaultAsync(
            v => v.ModifierOptionId == modifierOptionId && v.Version == targetVersion,
            cancellationToken: cancellationToken);

        if (targetVersionSnapshot == null)
        {
            throw new InvalidOperationException($"Version {targetVersion} not found for modifier option {modifierOptionId}");
        }

        if (!targetVersionSnapshot.CanRollback())
        {
            throw new InvalidOperationException($"Cannot rollback to version {targetVersion} - too old or restricted");
        }

        // Rollback properties
        modifierOption.UpdateProperties(
            targetVersionSnapshot.Name,
            targetVersionSnapshot.NameLocalized,
            targetVersionSnapshot.ImageUrl,
            targetVersionSnapshot.IsActive,
            targetVersionSnapshot.SortOrder);

        // Rollback price
        modifierOption.UpdatePrice(targetVersionSnapshot.Price, $"Rollback to version {targetVersion}: {reason}");

        await _modifierOptionRepository.UpdateAsync(modifierOption, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogWarning(
            "Rolled back modifier option {OptionId} from version {CurrentVersion} to version {TargetVersion}. Reason: {Reason}",
            modifierOptionId, modifierOption.Version, targetVersion, reason ?? "Not specified");

        return modifierOption;
    }

    #region Private Methods

    private List<FoodicsModifierDto> ExtractModifiersFromProducts(List<FoodicsProductDetailDto> products)
    {
        var modifiers = new Dictionary<string, FoodicsModifierDto>();

        foreach (var product in products)
        {
            if (product.Modifiers != null)
            {
                foreach (var modifier in product.Modifiers)
                {
                    if (!modifiers.ContainsKey(modifier.Id))
                    {
                        modifiers[modifier.Id] = modifier;
                    }
                }
            }
        }

        return modifiers.Values.ToList();
    }

    private async Task<ModifierGroupSyncResult> SyncModifierGroupAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId,
        FoodicsModifierDto modifierData,
        Dictionary<string, ModifierGroup> existingGroups,
        CancellationToken cancellationToken)
    {
        var result = new ModifierGroupSyncResult();
        var structureHash = CalculateModifierGroupHash(modifierData.Name ?? "", modifierData.MinAllowed, modifierData.MaxAllowed, false);

        ModifierGroup modifierGroup;

        if (existingGroups.TryGetValue(modifierData.Id, out var existingGroup))
        {
            // Update existing group
            if (existingGroup.StructureHash != structureHash)
            {
                existingGroup.UpdateStructure(
                    modifierData.Name ?? "",
                    modifierData.NameLocalized,
                    modifierData.MinAllowed,
                    modifierData.MaxAllowed,
                    false, // IsRequired not available in DTO
                    structureHash);
                
                result.WasUpdated = true;
            }
            modifierGroup = existingGroup;
        }
        else
        {
            // Create new group
            modifierGroup = new ModifierGroup
            {
                FoodicsAccountId = foodicsAccountId,
                BranchId = branchId,
                MenuGroupId = menuGroupId,
                FoodicsModifierGroupId = modifierData.Id,
                Name = modifierData.Name ?? "",
                NameLocalized = modifierData.NameLocalized,
                MinSelection = modifierData.MinAllowed,
                MaxSelection = modifierData.MaxAllowed,
                IsRequired = false,
                IsActive = true,
                StructureHash = structureHash,
                LastUpdatedAt = DateTime.UtcNow
            };

            modifierGroup = await _modifierGroupRepository.InsertAsync(modifierGroup, autoSave: true, cancellationToken: cancellationToken);
            result.WasCreated = true;
        }

        // Sync options
        if (modifierData.Options != null)
        {
            var optionSyncResult = await SyncModifierOptionsAsync(modifierGroup, modifierData.Options, cancellationToken);
            result.OptionsCreated = optionSyncResult.OptionsCreated;
            result.OptionsUpdated = optionSyncResult.OptionsUpdated;
            result.PriceChanges = optionSyncResult.PriceChanges;
        }

        return result;
    }

    private async Task<ModifierOptionSyncResult> SyncModifierOptionsAsync(
        ModifierGroup modifierGroup,
        List<FoodicsModifierOptionDto> optionData,
        CancellationToken cancellationToken)
    {
        var result = new ModifierOptionSyncResult();
        var existingOptions = await GetModifierOptionsAsync(modifierGroup.Id, false, cancellationToken);
        var existingOptionsDict = existingOptions.ToDictionary(o => o.FoodicsModifierOptionId, o => o);

        foreach (var optionDto in optionData)
        {
            if (existingOptionsDict.TryGetValue(optionDto.Id, out var existingOption))
            {
                // Update existing option
                var hasChanges = false;

                // Check for property changes
                if (existingOption.Name != (optionDto.Name ?? "") ||
                    existingOption.NameLocalized != optionDto.NameLocalized ||
                    existingOption.ImageUrl != optionDto.Image)
                {
                    existingOption.UpdateProperties(
                        optionDto.Name ?? "",
                        optionDto.NameLocalized,
                        optionDto.Image,
                        existingOption.IsActive,
                        existingOption.SortOrder);
                    hasChanges = true;
                }

                // Check for price changes
                if (optionDto.Price.HasValue && existingOption.Price != optionDto.Price.Value)
                {
                    existingOption.UpdatePrice(optionDto.Price.Value, "Foodics sync");
                    result.PriceChanges++;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await _modifierOptionRepository.UpdateAsync(existingOption, autoSave: true, cancellationToken: cancellationToken);
                    result.OptionsUpdated++;
                }
            }
            else
            {
                // Create new option
                var newOption = new ModifierOption
                {
                    ModifierGroupId = modifierGroup.Id,
                    FoodicsModifierOptionId = optionDto.Id,
                    Name = optionDto.Name ?? "",
                    NameLocalized = optionDto.NameLocalized,
                    Price = optionDto.Price ?? 0,
                    IsActive = true,
                    ImageUrl = optionDto.Image,
                    LastUpdatedAt = DateTime.UtcNow
                };

                await _modifierOptionRepository.InsertAsync(newOption, autoSave: true, cancellationToken: cancellationToken);
                result.OptionsCreated++;
            }
        }

        return result;
    }

    private async Task UpdateProductModifierAssignmentsAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId,
        List<FoodicsProductDetailDto> products,
        CancellationToken cancellationToken)
    {
        // Get existing assignments
        var existingAssignments = await _assignmentRepository.GetListAsync(
            a => a.FoodicsAccountId == foodicsAccountId &&
                 a.BranchId == branchId &&
                 a.MenuGroupId == menuGroupId,
            cancellationToken: cancellationToken);

        var existingAssignmentsDict = existingAssignments
            .GroupBy(a => a.FoodicsProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get modifier groups
        var modifierGroups = await GetModifierGroupsAsync(foodicsAccountId, branchId, menuGroupId, true, cancellationToken);
        var modifierGroupsDict = modifierGroups.ToDictionary(mg => mg.FoodicsModifierGroupId, mg => mg);

        foreach (var product in products)
        {
            if (product.Modifiers == null || !product.Modifiers.Any())
                continue;

            var currentAssignments = existingAssignmentsDict.GetValueOrDefault(product.Id, new List<ProductModifierAssignment>());
            var currentModifierIds = product.Modifiers.Select(m => m.Id).ToHashSet();

            // Deactivate removed assignments
            foreach (var assignment in currentAssignments)
            {
                if (modifierGroupsDict.TryGetValue(assignment.ModifierGroup.FoodicsModifierGroupId, out var group) &&
                    !currentModifierIds.Contains(group.FoodicsModifierGroupId))
                {
                    assignment.Deactivate();
                }
            }

            // Create or activate assignments
            var sortOrder = 0;
            foreach (var modifier in product.Modifiers)
            {
                if (modifierGroupsDict.TryGetValue(modifier.Id, out var modifierGroup))
                {
                    var existingAssignment = currentAssignments.FirstOrDefault(a => a.ModifierGroupId == modifierGroup.Id);
                    
                    if (existingAssignment != null)
                    {
                        existingAssignment.Activate();
                        existingAssignment.UpdateSortOrder(sortOrder);
                    }
                    else
                    {
                        var newAssignment = new ProductModifierAssignment
                        {
                            FoodicsAccountId = foodicsAccountId,
                            BranchId = branchId,
                            MenuGroupId = menuGroupId,
                            FoodicsProductId = product.Id,
                            ModifierGroupId = modifierGroup.Id,
                            SortOrder = sortOrder,
                            AssignedAt = DateTime.UtcNow,
                            LastUpdatedAt = DateTime.UtcNow
                        };

                        await _assignmentRepository.InsertAsync(newAssignment, autoSave: false, cancellationToken: cancellationToken);
                    }
                }
                sortOrder++;
            }
        }

        await _assignmentRepository.GetDbContext().SaveChangesAsync(cancellationToken);
    }

    private async Task DetectOptionChangesAsync(
        ModifierGroup existingGroup,
        FoodicsModifierDto currentModifier,
        ModifierChangeDetectionResult result,
        CancellationToken cancellationToken)
    {
        if (currentModifier.Options == null)
            return;

        var existingOptions = await GetModifierOptionsAsync(existingGroup.Id, false, cancellationToken);
        var existingOptionsDict = existingOptions.ToDictionary(o => o.FoodicsModifierOptionId, o => o);

        foreach (var currentOption in currentModifier.Options)
        {
            if (existingOptionsDict.TryGetValue(currentOption.Id, out var existingOption))
            {
                // Check for price changes
                if (currentOption.Price.HasValue && existingOption.Price != currentOption.Price.Value)
                {
                    result.PriceChanges.Add(new ModifierPriceChange
                    {
                        ModifierOptionId = existingOption.Id,
                        FoodicsModifierOptionId = currentOption.Id,
                        OldPrice = existingOption.Price,
                        NewPrice = currentOption.Price.Value,
                        ChangePercentage = existingOption.Price != 0 
                            ? ((currentOption.Price.Value - existingOption.Price) / existingOption.Price) * 100 
                            : 0
                    });
                }
            }
        }
    }

    private string CalculateModifierGroupHash(string name, int? minSelection, int? maxSelection, bool isRequired)
    {
        var hashInput = $"{name}|{minSelection}|{maxSelection}|{isRequired}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))
            .Take(32).Aggregate("", (s, b) => s + b.ToString("x2"));
    }

    #endregion
}

#region Result Classes

public class ModifierSyncResult
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public Guid? MenuGroupId { get; set; }
    public int TotalModifiersFound { get; set; }
    public int ModifierGroupsCreated { get; set; }
    public int ModifierGroupsUpdated { get; set; }
    public int ModifierOptionsCreated { get; set; }
    public int ModifierOptionsUpdated { get; set; }
    public int PriceChanges { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ModifierGroupSyncResult
{
    public bool WasCreated { get; set; }
    public bool WasUpdated { get; set; }
    public int OptionsCreated { get; set; }
    public int OptionsUpdated { get; set; }
    public int PriceChanges { get; set; }
}

public class ModifierOptionSyncResult
{
    public int OptionsCreated { get; set; }
    public int OptionsUpdated { get; set; }
    public int PriceChanges { get; set; }
}

public class ModifierChangeDetectionResult
{
    public DateTime DetectedAt { get; set; }
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public Guid? MenuGroupId { get; set; }
    public bool HasChanges { get; set; }
    public List<string> NewGroups { get; set; } = new();
    public List<string> RemovedGroups { get; set; } = new();
    public List<ModifierGroupChange> ModifiedGroups { get; set; } = new();
    public List<ModifierPriceChange> PriceChanges { get; set; } = new();
}

public class ModifierGroupChange
{
    public Guid ModifierGroupId { get; set; }
    public string FoodicsModifierGroupId { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class ModifierPriceChange
{
    public Guid ModifierOptionId { get; set; }
    public string FoodicsModifierOptionId { get; set; } = string.Empty;
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public decimal ChangePercentage { get; set; }
}

public class ModifierValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ModifierAnalyticsResult
{
    public DateTime GeneratedAt { get; set; }
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public Guid? MenuGroupId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalModifierGroups { get; set; }
    public int ActiveModifierGroups { get; set; }
    public int TotalModifierOptions { get; set; }
    public int ActiveModifierOptions { get; set; }
    public int TotalPriceChanges { get; set; }
    public int PriceIncreases { get; set; }
    public int PriceDecreases { get; set; }
    public decimal AveragePriceChange { get; set; }
}

#endregion