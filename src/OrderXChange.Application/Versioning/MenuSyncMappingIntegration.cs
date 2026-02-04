using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Integration service that bridges existing sync services with the new stable ID-based mapping system
/// Provides backward compatibility while enabling stable remote code usage
/// </summary>
public class MenuSyncMappingIntegration : ITransientDependency
{
    private readonly IMenuMappingService _menuMappingService;
    private readonly FoodicsToTalabatMapper _talabatMapper;
    private readonly ILogger<MenuSyncMappingIntegration> _logger;

    public MenuSyncMappingIntegration(
        IMenuMappingService menuMappingService,
        FoodicsToTalabatMapper talabatMapper,
        ILogger<MenuSyncMappingIntegration> logger)
    {
        _menuMappingService = menuMappingService;
        _talabatMapper = talabatMapper;
        _logger = logger;
    }

    /// <summary>
    /// Maps Foodics products to Talabat catalog using stable ID-based mapping
    /// Automatically handles mapping creation and provides fallback to legacy mapping
    /// </summary>
    public async Task<TalabatCatalogSubmitRequest> MapToTalabatCatalogWithStableIdsAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId,
        string vendorCode,
        string? callbackUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to use stable ID-based mapping
            return await _talabatMapper.MapToTalabatCatalogAsync(
                products, foodicsAccountId, branchId, vendorCode, callbackUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Failed to use stable ID-based mapping, falling back to legacy mapping. AccountId={AccountId}, VendorCode={VendorCode}",
                foodicsAccountId, vendorCode);

            // Fallback to legacy mapping
            return _talabatMapper.MapToTalabatCatalog(products, vendorCode, callbackUrl);
        }
    }

    /// <summary>
    /// Maps Foodics products to Talabat V2 catalog using stable ID-based mapping
    /// Automatically handles mapping creation and provides fallback to legacy mapping
    /// </summary>
    public async Task<TalabatV2CatalogSubmitRequest> MapToTalabatV2CatalogWithStableIdsAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId,
        string chainCode,
        string? callbackUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to use stable ID-based mapping
            // Note: vendorCode is not available in this context, will use configuration fallback
            return await _talabatMapper.MapToTalabatV2CatalogAsync(
                products, foodicsAccountId, branchId, chainCode, vendorCode: null, callbackUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Failed to use stable ID-based V2 mapping, falling back to legacy mapping. AccountId={AccountId}, ChainCode={ChainCode}",
                foodicsAccountId, chainCode);

            // Fallback to legacy mapping
            // Note: vendorCode is not available in this context, will use configuration fallback
            return _talabatMapper.MapToTalabatV2Catalog(products, chainCode, vendorCode: null, callbackUrl);
        }
    }

    /// <summary>
    /// Maps item availability updates using stable remote codes
    /// Automatically handles mapping lookup and provides fallback to legacy mapping
    /// </summary>
    public async Task<TalabatUpdateItemAvailabilityRequest> MapToItemAvailabilityUpdateWithStableIdsAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to use stable ID-based mapping
            return await _talabatMapper.MapToItemAvailabilityUpdateAsync(
                products, foodicsAccountId, branchId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Failed to use stable ID-based availability mapping, falling back to legacy mapping. AccountId={AccountId}",
                foodicsAccountId);

            // Fallback to legacy mapping
            return _talabatMapper.MapToItemAvailabilityUpdate(products);
        }
    }

    /// <summary>
    /// Ensures all mappings exist for a set of products before sync
    /// This is useful for pre-warming the mapping cache before large sync operations
    /// </summary>
    public async Task<Dictionary<string, MenuItemMapping>> EnsureMappingsExistAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        var productsList = products.ToList();
        
        _logger.LogInformation(
            "Ensuring mappings exist for {ProductCount} products. AccountId={AccountId}, BranchId={BranchId}",
            productsList.Count, foodicsAccountId, branchId ?? "ALL");

        try
        {
            var mappings = await _menuMappingService.BulkCreateOrUpdateMappingsAsync(
                foodicsAccountId, branchId, productsList, cancellationToken);

            _logger.LogInformation(
                "Successfully ensured {MappingCount} mappings exist",
                mappings.Count);

            return mappings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to ensure mappings exist. AccountId={AccountId}, BranchId={BranchId}",
                foodicsAccountId, branchId);
            throw;
        }
    }

    /// <summary>
    /// Gets stable remote codes for a list of Foodics IDs
    /// Useful for converting existing data to use stable remote codes
    /// </summary>
    public async Task<Dictionary<string, string>> GetStableRemoteCodesAsync(
        IEnumerable<string> foodicsIds,
        string entityType,
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();
        
        foreach (var foodicsId in foodicsIds)
        {
            var mapping = await _menuMappingService.GetMappingByFoodicsIdAsync(
                foodicsAccountId, branchId, entityType, foodicsId, cancellationToken);

            if (mapping != null)
            {
                result[foodicsId] = mapping.TalabatRemoteCode;
            }
            else
            {
                _logger.LogWarning(
                    "No mapping found for FoodicsId={FoodicsId}, EntityType={EntityType}",
                    foodicsId, entityType);
            }
        }

        return result;
    }

    /// <summary>
    /// Validates mapping integrity for an account/branch
    /// Useful for maintenance and troubleshooting
    /// </summary>
    public async Task<MappingValidationResult> ValidateMappingIntegrityAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Validating mapping integrity. AccountId={AccountId}, BranchId={BranchId}",
            foodicsAccountId, branchId ?? "ALL");

        try
        {
            var result = await _menuMappingService.ValidateAndFixMappingsAsync(
                foodicsAccountId, branchId, cancellationToken);

            _logger.LogInformation(
                "Mapping validation completed. Valid={Valid}, Fixed={Fixed}, Issues={Issues}",
                result.ValidMappings, result.FixedMappings, result.Issues.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to validate mapping integrity. AccountId={AccountId}, BranchId={BranchId}",
                foodicsAccountId, branchId);
            throw;
        }
    }

    /// <summary>
    /// Gets mapping statistics for monitoring and reporting
    /// </summary>
    public async Task<MappingStatistics> GetMappingStatisticsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        return await _menuMappingService.GetMappingStatisticsAsync(
            foodicsAccountId, branchId, cancellationToken);
    }

    /// <summary>
    /// Updates Talabat internal IDs after successful catalog import
    /// This should be called when receiving webhook notifications from Talabat
    /// </summary>
    public async Task<int> UpdateTalabatInternalIdsAsync(
        Dictionary<string, string> remoteCodeToInternalIdMap,
        CancellationToken cancellationToken = default)
    {
        if (!remoteCodeToInternalIdMap.Any())
            return 0;

        _logger.LogInformation(
            "Updating Talabat internal IDs for {Count} mappings",
            remoteCodeToInternalIdMap.Count);

        try
        {
            var updatedCount = await _menuMappingService.UpdateTalabatInternalIdsAsync(
                remoteCodeToInternalIdMap, cancellationToken);

            _logger.LogInformation(
                "Successfully updated {Count} Talabat internal IDs",
                updatedCount);

            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Talabat internal IDs");
            throw;
        }
    }

    /// <summary>
    /// Deactivates mappings for items that no longer exist in Foodics
    /// This should be called during cleanup operations
    /// </summary>
    public async Task<int> DeactivateObsoleteMappingsAsync(
        Guid foodicsAccountId,
        string? branchId,
        HashSet<string> currentFoodicsIds,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Deactivating obsolete {EntityType} mappings. AccountId={AccountId}, BranchId={BranchId}, CurrentCount={Count}",
            entityType, foodicsAccountId, branchId ?? "ALL", currentFoodicsIds.Count);

        try
        {
            var deactivatedCount = await _menuMappingService.DeactivateObsoleteMappingsAsync(
                foodicsAccountId, branchId, currentFoodicsIds, entityType, cancellationToken);

            _logger.LogInformation(
                "Successfully deactivated {Count} obsolete {EntityType} mappings",
                deactivatedCount, entityType);

            return deactivatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to deactivate obsolete mappings. EntityType={EntityType}",
                entityType);
            throw;
        }
    }
}