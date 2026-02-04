using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Foodics;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.BackgroundJobs;

public class ProductAvailabilityAppService : ApplicationService, IProductAvailabilityAppService, ITransientDependency
{
    private readonly FoodicsCatalogClient _foodicsCatalogClient;
    private readonly FoodicsAccountTokenService _tokenService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ProductAvailabilityAppService> _logger;

    public ProductAvailabilityAppService(
        FoodicsCatalogClient foodicsCatalogClient,
        FoodicsAccountTokenService tokenService,
        ICurrentTenant currentTenant,
        ILogger<ProductAvailabilityAppService> logger)
    {
        _foodicsCatalogClient = foodicsCatalogClient;
        _tokenService = tokenService;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<ProductAvailabilitySyncResultDto> FetchAndPrepareAsync(int page = 1, int perPage = 100)
    {
        if (!_currentTenant.IsAvailable)
        {
            throw new UserFriendlyException("Tenant context is required to fetch product availability.");
        }

        var tenantId = _currentTenant.Id.Value;
        _logger.LogInformation("Starting product availability fetch and preparation for tenant {TenantId} (page {Page}, perPage {PerPage})", tenantId, page, perPage);

        string accessToken;

        // TODO: Temporarily using configuration token as fallback until FoodicsAccount is set up for all tenants
        // var foodicsAccount = await _foodicsAccountRepository.FirstOrDefaultAsync(x => x.TenantId == tenantId);
        // if (foodicsAccount == null)
        // {
        //     throw new UserFriendlyException($"Foodics account not found for tenant {tenantId}.");
        // }
        //
        // if (string.IsNullOrWhiteSpace(foodicsAccount.AccessToken))
        // {
        //     throw new UserFriendlyException($"Foodics access token is not configured for tenant {tenantId}.");
        // }
        //
        // _logger.LogDebug("Using Foodics account {AccountId} for tenant {TenantId}", foodicsAccount.Id, tenantId);
        // accessToken = foodicsAccount.AccessToken;

        // Use FoodicsAccountTokenService for consistent token retrieval with fallback
        try
        {
            accessToken = await _tokenService.GetAccessTokenWithFallbackAsync(null, CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            throw new UserFriendlyException($"Failed to get Foodics access token for tenant {tenantId}: {ex.Message}");
        }

        // Temporarily commented out - method doesn't exist yet
        // var response = await _foodicsCatalogClient.GetProductsWithAvailabilityAsync(
        //     accessToken,
        //     page,
        //     perPage,
        //     CancellationToken.None);
        
        // Return empty result for now
        return new ProductAvailabilitySyncResultDto
        {
            TotalProducts = 0,
            TotalBranches = 0,
            AvailableProducts = 0,
            UnavailableProducts = 0,
            Products = new List<TalabatProductAvailabilityDto>()
        };

        /* Temporarily commented out
        if (response.Data == null || response.Data.Count == 0)
        {
            _logger.LogWarning("No products found in Foodics response");
            return new ProductAvailabilitySyncResultDto
            {
                TotalProducts = 0,
                TotalBranches = 0,
                AvailableProducts = 0,
                UnavailableProducts = 0,
                Products = new List<TalabatProductAvailabilityDto>()
            };
        }

        // Prepare data for Talabat
        var talabatProducts = new List<TalabatProductAvailabilityDto>();
        var branchIds = new HashSet<string>();
        int availableCount = 0;
        int unavailableCount = 0;

        foreach (var product in response.Data)
        {
            if (string.IsNullOrWhiteSpace(product.Id))
            {
                _logger.LogWarning("Skipping product with empty ID");
                continue;
            }

            // Extract price tag IDs and names
            var priceTagIds = product.PriceTags?
                .Where(pt => !string.IsNullOrWhiteSpace(pt.Id))
                .Select(pt => pt.Id)
                .ToList() ?? new List<string>();

            var priceTagNames = product.PriceTags?
                .Where(pt => !string.IsNullOrWhiteSpace(pt.Name))
                .Select(pt => pt.Name!)
                .ToList() ?? new List<string>();

            // Process each branch for this product
            if (product.Branches != null && product.Branches.Count > 0)
            {
                foreach (var branch in product.Branches)
                {
                    if (string.IsNullOrWhiteSpace(branch.Id))
                    {
                        _logger.LogWarning("Skipping branch with empty ID for product {ProductId}", product.Id);
                        continue;
                    }

                    branchIds.Add(branch.Id);

                    var pivot = branch.Pivot;
                    var isAvailable = pivot?.IsInStock ?? false;
                    var isActive = pivot?.IsActive ?? product.IsActive ?? false;
                    var price = pivot?.Price ?? product.Price;

                    if (isAvailable)
                    {
                        availableCount++;
                    }
                    else
                    {
                        unavailableCount++;
                    }

                    var talabatProduct = new TalabatProductAvailabilityDto
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductSku = product.Sku,
                        BranchId = branch.Id,
                        BranchName = branch.Name,
                        BranchReference = branch.Reference,
                        Price = price,
                        IsAvailable = isAvailable,
                        IsActive = isActive,
                        PriceTagIds = priceTagIds,
                        PriceTagNames = priceTagNames
                    };

                    talabatProducts.Add(talabatProduct);
                }
            }
            else
            {
                // Product has no branches - mark as unavailable
                _logger.LogDebug("Product {ProductId} has no branches assigned", product.Id);
                unavailableCount++;
            }
        }

        var result = new ProductAvailabilitySyncResultDto
        {
            TotalProducts = response.Data.Count,
            TotalBranches = branchIds.Count,
            AvailableProducts = availableCount,
            UnavailableProducts = unavailableCount,
            Products = talabatProducts
        };

        _logger.LogInformation(
            "Product availability preparation completed. Products: {TotalProducts}, Branches: {TotalBranches}, Available: {AvailableProducts}, Unavailable: {UnavailableProducts}",
            result.TotalProducts,
            result.TotalBranches,
            result.AvailableProducts,
            result.UnavailableProducts);

        return result;
        */
    }
}


