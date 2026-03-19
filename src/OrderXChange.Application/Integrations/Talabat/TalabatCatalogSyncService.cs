using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Staging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Talabat;

/// <summary>
/// Service for synchronizing catalog from Foodics to Talabat
/// Orchestrates the mapping and submission process
/// </summary>
public class TalabatCatalogSyncService : ITransientDependency
{
    private readonly TalabatCatalogClient _talabatCatalogClient;
    private readonly FoodicsToTalabatMapper _mapper;
    private readonly FoodicsMenuClient _foodicsMenuClient;
    private readonly TalabatSyncStatusService _syncStatusService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatCatalogSyncService> _logger;

    public TalabatCatalogSyncService(
        TalabatCatalogClient talabatCatalogClient,
        FoodicsToTalabatMapper mapper,
        FoodicsMenuClient foodicsMenuClient,
        TalabatSyncStatusService syncStatusService,
        IConfiguration configuration,
        ILogger<TalabatCatalogSyncService> logger)
    {
        _talabatCatalogClient = talabatCatalogClient;
        _mapper = mapper;
        _foodicsMenuClient = foodicsMenuClient;
        _syncStatusService = syncStatusService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Sync Foodics products to Talabat catalog
    /// </summary>
    /// <param name="products">Foodics products with full includes</param>
    /// <param name="chainCode">Talabat chain code (e.g., "tlbt-pick")</param>
    /// <param name="foodicsAccountId">FoodicsAccount ID for stable ID mapping</param>
    /// <param name="branchId">Optional branch ID for stable ID mapping</param>
    /// <param name="vendorCode">Talabat vendor code</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync result</returns>
    public async Task<TalabatSyncResult> SyncCatalogAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        string chainCode,
        Guid foodicsAccountId,
        string? branchId,
        string vendorCode,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= Guid.NewGuid().ToString();
        var originalProductsList = products.ToList();
        var orderingContext = await BuildFoodicsOrderingContextAsync(
            FilterProductsForTalabat(originalProductsList).ToList(),
            foodicsAccountId,
            branchId,
            cancellationToken);
        var productsList = orderingContext.Products;
        var categoriesCount = productsList
            .Select(p => p.Category?.Id ?? p.CategoryId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Count();

        if (productsList.Count != originalProductsList.Count)
        {
            _logger.LogInformation(
                "Filtered products for Talabat submission. Original={OriginalCount}, Filtered={FilteredCount}",
                originalProductsList.Count,
                productsList.Count);
        }

        _logger.LogInformation(
            "Starting Talabat catalog sync. CorrelationId={CorrelationId}, ChainCode={ChainCode}, VendorCode={VendorCode}, ProductCount={ProductCount}",
            correlationId,
            chainCode,
            vendorCode,
            productsList.Count);

        var result = new TalabatSyncResult
        {
            CorrelationId = correlationId,
            VendorCode = vendorCode,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Build callback URL
            var callbackBaseUrl = _configuration["Talabat:CallbackBaseUrl"];
            string? callbackUrl = null;
            if (!string.IsNullOrWhiteSpace(callbackBaseUrl))
            {
                callbackUrl = $"{callbackBaseUrl.TrimEnd('/')}/catalog-status";
            }

            // Map Foodics products to Talabat V2 catalog format (items-based)
            var catalogRequest = await _mapper.MapToTalabatV2CatalogAsync(
                productsList,
                foodicsAccountId,
                branchId,
                chainCode,
                vendorCode,
                callbackUrl,
                orderingContext.CategoryOrder,
                orderingContext.ProductOrder,
                cancellationToken);

            LogTalabatV2OrderingPreview(catalogRequest, correlationId, chainCode, vendorCode);

            result.CategoriesCount = categoriesCount;
            result.ProductsCount = productsList.Count;

            _logger.LogInformation(
                "Mapped V2 catalog for Talabat. CorrelationId={CorrelationId}, ChainCode={ChainCode}, Categories={Categories}, Products={Products}",
                correlationId,
                chainCode,
                result.CategoriesCount,
                result.ProductsCount);

            // Submit to Talabat V2 API
            var response = await _talabatCatalogClient.SubmitV2CatalogAsync(
                chainCode,
                catalogRequest,
                vendorCode,
                cancellationToken);

            result.Success = response.Success;
            result.ImportId = response.ImportId;
            result.Message = response.Message;
            result.CompletedAt = DateTime.UtcNow;

            if (response.Success)
            {
                _logger.LogInformation(
                    "Talabat catalog sync submitted successfully. CorrelationId={CorrelationId}, ImportId={ImportId}",
                    correlationId,
                    response.ImportId);
            }
            else
            {
                _logger.LogWarning(
                    "Talabat catalog sync submission failed. CorrelationId={CorrelationId}, Message={Message}, Errors={Errors}",
                    correlationId,
                    response.Message,
                    response.Errors != null ? string.Join("; ", response.Errors.Select(e => $"{e.Field}: {e.Message}")) : "<none>");

                result.Errors = response.Errors?.Select(e => $"{e.Field}: {e.Message}").ToList();
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Message = ex.Message;
            result.Errors = new List<string> { ex.ToString() };

            _logger.LogError(
                ex,
                "Error during Talabat catalog sync. CorrelationId={CorrelationId}, VendorCode={VendorCode}",
                correlationId,
                vendorCode);
        }

        return result;
    }

    /// <summary>
    /// Update item availability on Talabat
    /// </summary>
    public async Task<TalabatSyncResult> UpdateItemAvailabilityAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        string vendorCode,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= Guid.NewGuid().ToString();
        var productsList = products.ToList();

        _logger.LogInformation(
            "Updating item availability on Talabat. CorrelationId={CorrelationId}, VendorCode={VendorCode}, ItemCount={ItemCount}",
            correlationId,
            vendorCode,
            productsList.Count);

        var result = new TalabatSyncResult
        {
            CorrelationId = correlationId,
            VendorCode = vendorCode,
            StartedAt = DateTime.UtcNow,
            ProductsCount = productsList.Count
        };

        try
        {
            var request = _mapper.MapToItemAvailabilityUpdate(productsList);
            var response = await _talabatCatalogClient.UpdateItemAvailabilityAsync(
                vendorCode,
                request,
                cancellationToken);

            result.Success = response.Success;
            result.Message = response.Message;
            result.CompletedAt = DateTime.UtcNow;

            if (!response.Success && response.Errors != null)
            {
                result.Errors = response.Errors.Select(e => $"{e.Field}: {e.Message}").ToList();
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Message = ex.Message;
            result.Errors = new List<string> { ex.ToString() };

            _logger.LogError(
                ex,
                "Error updating item availability on Talabat. CorrelationId={CorrelationId}",
                correlationId);
        }

        return result;
    }

    /// <summary>
    /// Update vendor/store availability on Talabat
    /// </summary>
    public async Task<TalabatSyncResult> UpdateVendorAvailabilityAsync(
        string vendorCode,
        bool isAvailable,
        string? reason = null,
        DateTime? availableAt = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Updating vendor availability on Talabat. CorrelationId={CorrelationId}, VendorCode={VendorCode}, IsAvailable={IsAvailable}",
            correlationId,
            vendorCode,
            isAvailable);

        var result = new TalabatSyncResult
        {
            CorrelationId = correlationId,
            VendorCode = vendorCode,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var request = _mapper.MapToVendorAvailability(isAvailable, reason, availableAt);
            var response = await _talabatCatalogClient.UpdateVendorAvailabilityAsync(
                vendorCode,
                request,
                cancellationToken);

            result.Success = response != null;
            result.Message = response != null ? "Vendor availability updated" : "Failed to update vendor availability";
            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Message = ex.Message;
            result.Errors = new List<string> { ex.ToString() };

            _logger.LogError(
                ex,
                "Error updating vendor availability on Talabat. CorrelationId={CorrelationId}",
                correlationId);
        }

        return result;
    }

    /// <summary>
    /// Get catalog import status from Talabat
    /// </summary>
    public async Task<TalabatCatalogImportLogResponse?> GetImportStatusAsync(
        string vendorCode,
        string? importId = null,
        CancellationToken cancellationToken = default)
    {
        return await _talabatCatalogClient.GetCatalogImportLogAsync(
            vendorCode,
            importId,
            cancellationToken);
    }

    /// <summary>
    /// Sync Foodics products to Talabat using V2 API (items-based structure)
    /// This is the RECOMMENDED method for new integrations
    /// 
    /// FIXED: Now accepts vendorCode to get token BEFORE HTTP request to avoid DbContext disposal
    /// </summary>
    /// <param name="products">Foodics products with full includes</param>
    /// <param name="chainCode">Talabat chain code (e.g., "tlbt-pick")</param>
    /// <param name="foodicsAccountId">FoodicsAccount ID for tracking</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="vendorCode">Optional vendor code to get credentials for specific TalabatAccount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync result</returns>
    public async Task<TalabatSyncResult> SyncCatalogV2Async(
        IEnumerable<FoodicsProductDetailDto> products,
        string chainCode,
        Guid foodicsAccountId,
        string? branchId = null,
        string? correlationId = null,
        string? vendorCode = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= Guid.NewGuid().ToString();
        var originalProductsList = products.ToList();
        var orderingContext = await BuildFoodicsOrderingContextAsync(
            FilterProductsForTalabat(originalProductsList).ToList(),
            foodicsAccountId,
            branchId,
            cancellationToken);
        var productsList = orderingContext.Products;
        var categoriesCount = productsList
            .Select(p => p.Category?.Id ?? p.CategoryId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Count();

        if (productsList.Count != originalProductsList.Count)
        {
            _logger.LogInformation(
                "Filtered products for Talabat submission. Original={OriginalCount}, Filtered={FilteredCount}",
                originalProductsList.Count,
                productsList.Count);
        }

        _logger.LogInformation(
            "Starting Talabat V2 catalog sync. CorrelationId={CorrelationId}, ChainCode={ChainCode}, ProductCount={ProductCount}",
            correlationId,
            chainCode,
            productsList.Count);

        var result = new TalabatSyncResult
        {
            CorrelationId = correlationId,
            VendorCode = chainCode,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Build callback URL for webhook notifications
            var callbackBaseUrl = _configuration["Talabat:CallbackBaseUrl"];
            string? callbackUrl = null;
            if (!string.IsNullOrWhiteSpace(callbackBaseUrl))
            {
                callbackUrl = $"{callbackBaseUrl.TrimEnd('/')}/catalog-status";
                _logger.LogInformation(
                    "Configured webhook callback URL: {CallbackUrl}",
                    callbackUrl);
            }
            else
            {
                _logger.LogWarning("No webhook callback URL configured (Talabat:CallbackBaseUrl is empty)");
            }

            // Map Foodics products to Talabat V2 catalog format
            // Pass vendorCode to ensure each TalabatAccount gets its own vendor in the vendors array
            var catalogRequest = await _mapper.MapToTalabatV2CatalogAsync(
                productsList,
                foodicsAccountId,
                branchId,
                chainCode,
                vendorCode,
                callbackUrl,
                orderingContext.CategoryOrder,
                orderingContext.ProductOrder,
                cancellationToken);

            result.CategoriesCount = categoriesCount;
            result.ProductsCount = productsList.Count;

            _logger.LogInformation(
                "Mapped catalog to Talabat V2 format. CorrelationId={CorrelationId}, Categories={Categories}, Products={Products}",
                correlationId,
                result.CategoriesCount,
                result.ProductsCount);

                var response = await _talabatCatalogClient.SubmitV2CatalogAsync(
                chainCode,
                catalogRequest,
                vendorCode,  // ✅ NEW: Pass vendorCode to avoid DbContext disposal during HTTP request
                cancellationToken);

            result.Success = response.Success;
            result.ImportId = response.ImportId;
            result.Message = response.Message;
            result.CompletedAt = DateTime.UtcNow;

            if (response.Success && !string.IsNullOrWhiteSpace(response.ImportId))
            {
                _logger.LogInformation(
                    "Talabat V2 catalog submitted successfully. CorrelationId={CorrelationId}, ImportId={ImportId}, Duration={Duration}ms",
                    correlationId,
                    response.ImportId,
                    result.Duration?.TotalMilliseconds ?? 0);

                // Record submission in database
                try
                {
                    var vendorCodes = catalogRequest.Vendors ?? new List<string>();
                    var vendorCodeStr = vendorCodes.Count > 0 ? vendorCodes[0] : chainCode;

                    await _syncStatusService.RecordSubmissionAsync(
                        foodicsAccountId,
                        vendorCodeStr,
                        chainCode,
                        response.ImportId,
                        correlationId,
                        result.CategoriesCount,
                        result.ProductsCount,
                        callbackUrl,
                        "V2",
                        cancellationToken);

                    _logger.LogInformation(
                        "Recorded Talabat submission in database. ImportId={ImportId}",
                        response.ImportId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to record Talabat submission in database. ImportId={ImportId}, Error={Error}",
                        response.ImportId,
                        ex.Message);
                    // Don't fail the entire operation
                }
            }
            else
            {
                _logger.LogWarning(
                    "Talabat V2 catalog submission failed. CorrelationId={CorrelationId}, Message={Message}, Errors={Errors}",
                    correlationId,
                    response.Message,
                    response.Errors != null ? string.Join("; ", response.Errors.Select(e => $"{e.Field}: {e.Message}")) : "<none>");

                result.Errors = response.Errors?.Select(e => $"{e.Field}: {e.Message}").ToList();
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Message = ex.Message;
            result.Errors = new List<string> { ex.ToString() };

            _logger.LogError(
                ex,
                "Error during Talabat V2 catalog sync. CorrelationId={CorrelationId}, ChainCode={ChainCode}",
                correlationId,
                chainCode);
        }

        return result;
    }

    private void LogTalabatV2OrderingPreview(
        TalabatV2CatalogSubmitRequest catalogRequest,
        string correlationId,
        string chainCode,
        string vendorCode)
    {
        try
        {
            var items = catalogRequest.Catalog?.Items;
            if (items == null || items.Count == 0)
            {
                _logger.LogWarning(
                    "Talabat V2 ordering preview skipped because catalog items are empty. CorrelationId={CorrelationId}, ChainCode={ChainCode}, VendorCode={VendorCode}",
                    correlationId,
                    chainCode,
                    vendorCode);
                return;
            }

            var menuItem = items.Values.FirstOrDefault(i => string.Equals(i.Type, "Menu", StringComparison.OrdinalIgnoreCase));
            if (menuItem?.Products == null || menuItem.Products.Count == 0)
            {
                _logger.LogWarning(
                    "Talabat V2 ordering preview skipped because menu item has no category references. CorrelationId={CorrelationId}, ChainCode={ChainCode}, VendorCode={VendorCode}",
                    correlationId,
                    chainCode,
                    vendorCode);
                return;
            }

            var orderedCategoryRefs = menuItem.Products.Values
                .OrderBy(x => x.Order ?? int.MaxValue)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categoryPreview = orderedCategoryRefs
                .Take(15)
                .Select(reference =>
                {
                    items.TryGetValue(reference.Id, out var categoryItem);
                    return new
                    {
                        reference.Id,
                        reference.Order,
                        Title = categoryItem?.Title?.Default,
                        ProductCount = categoryItem?.Products?.Count ?? 0
                    };
                })
                .ToList();

            var firstCategoryProductPreview = new List<object>();
            foreach (var categoryRef in orderedCategoryRefs.Take(5))
            {
                if (!items.TryGetValue(categoryRef.Id, out var categoryItem) || categoryItem?.Products == null)
                {
                    continue;
                }

                var products = categoryItem.Products.Values
                    .OrderBy(x => x.Order ?? int.MaxValue)
                    .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .Select(reference =>
                    {
                        items.TryGetValue(reference.Id, out var productItem);
                        return new
                        {
                            CategoryId = categoryRef.Id,
                            CategoryTitle = categoryItem.Title?.Default,
                            ProductId = reference.Id,
                            reference.Order,
                            Title = productItem?.Title?.Default
                        };
                    });

                firstCategoryProductPreview.AddRange(products);
            }

            _logger.LogWarning(
                "Talabat V2 ordering preview. CorrelationId={CorrelationId}, ChainCode={ChainCode}, VendorCode={VendorCode}, Categories={CategoriesJson}, Products={ProductsJson}",
                correlationId,
                chainCode,
                vendorCode,
                JsonSerializer.Serialize(categoryPreview),
                JsonSerializer.Serialize(firstCategoryProductPreview));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to log Talabat V2 ordering preview. CorrelationId={CorrelationId}, ChainCode={ChainCode}, VendorCode={VendorCode}",
                correlationId,
                chainCode,
                vendorCode);
        }
    }

    private async Task<FoodicsOrderingContext> BuildFoodicsOrderingContextAsync(
        List<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken)
    {
        if (products.Count <= 1)
        {
            return FoodicsOrderingContext.Create(
                products,
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            var menuDisplay = await _foodicsMenuClient.GetMenuAsync(
                branchId,
                foodicsAccountId: foodicsAccountId,
                cancellationToken: cancellationToken);

            if (menuDisplay?.Data?.Categories == null || menuDisplay.Data.Categories.Count == 0)
            {
                _logger.LogInformation(
                    "Foodics menu_display returned no categories. Keeping existing product order. FoodicsAccountId={FoodicsAccountId}, BranchId={BranchId}",
                    foodicsAccountId,
                    branchId ?? "<all>");
                return FoodicsOrderingContext.Create(
                    products,
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            }

            var categoryOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var productOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < menuDisplay.Data.Categories.Count; i++)
            {
                var category = menuDisplay.Data.Categories[i];
                if (!string.IsNullOrWhiteSpace(category.CategoryId) && !categoryOrder.ContainsKey(category.CategoryId))
                {
                    categoryOrder[category.CategoryId] = i;
                }

                var flattenedProductIds = FlattenProductIds(category.Children);
                for (var j = 0; j < flattenedProductIds.Count; j++)
                {
                    var productId = flattenedProductIds[j];
                    if (!productOrder.ContainsKey(productId))
                    {
                        productOrder[productId] = j;
                    }
                }
            }

            var ordered = products
                .Select((product, index) => new
                {
                    Product = product,
                    OriginalIndex = index,
                    CategoryOrder = ResolveCategoryOrder(product, categoryOrder),
                    ProductOrder = ResolveProductOrder(product, productOrder)
                })
                .OrderBy(x => x.CategoryOrder)
                .ThenBy(x => x.ProductOrder)
                .ThenBy(x => x.OriginalIndex)
                .Select(x => x.Product)
                .ToList();

            _logger.LogInformation(
                "Applied Foodics menu_display ordering to Talabat sync payload. FoodicsAccountId={FoodicsAccountId}, BranchId={BranchId}, Products={ProductCount}, OrderedCategories={CategoryCount}, OrderedProducts={OrderedProductCount}",
                foodicsAccountId,
                branchId ?? "<all>",
                ordered.Count,
                categoryOrder.Count,
                productOrder.Count);

            return FoodicsOrderingContext.Create(ordered, categoryOrder, productOrder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to apply Foodics menu_display ordering. Falling back to current product order. FoodicsAccountId={FoodicsAccountId}, BranchId={BranchId}",
                foodicsAccountId,
                branchId ?? "<all>");
            return FoodicsOrderingContext.Create(
                products,
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static int ResolveCategoryOrder(
        FoodicsProductDetailDto product,
        Dictionary<string, int> categoryOrder)
    {
        var categoryId = product.Category?.Id ?? product.CategoryId;
        return !string.IsNullOrWhiteSpace(categoryId) && categoryOrder.TryGetValue(categoryId, out var order)
            ? order
            : int.MaxValue;
    }

    private static int ResolveProductOrder(
        FoodicsProductDetailDto product,
        Dictionary<string, int> productOrder)
    {
        return !string.IsNullOrWhiteSpace(product.Id) && productOrder.TryGetValue(product.Id, out var order)
            ? order
            : int.MaxValue;
    }

    private static List<string> FlattenProductIds(IEnumerable<FoodicsMenuDisplayChildDto>? children)
    {
        var productIds = new List<string>();
        if (children == null)
        {
            return productIds;
        }

        foreach (var child in children)
        {
            if (string.Equals(child.ChildType, "product", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(child.ChildId))
            {
                productIds.Add(child.ChildId);
            }

            if (child.Children != null && child.Children.Count > 0)
            {
                productIds.AddRange(FlattenProductIds(child.Children));
            }
        }

        return productIds;
    }

    private static IEnumerable<FoodicsProductDetailDto> FilterProductsForTalabat(
        IEnumerable<FoodicsProductDetailDto> products)
    {
        return products.Where(p =>
            string.IsNullOrWhiteSpace(p.DeletedAt) &&
            (p.IsActive == true || (p.Groups != null && p.Groups.Count > 0)));
    }

    private sealed class FoodicsOrderingContext
    {
        public List<FoodicsProductDetailDto> Products { get; private set; } = new();
        public Dictionary<string, int> CategoryOrder { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> ProductOrder { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public static FoodicsOrderingContext Create(
            List<FoodicsProductDetailDto> products,
            Dictionary<string, int> categoryOrder,
            Dictionary<string, int> productOrder)
        {
            return new FoodicsOrderingContext
            {
                Products = products,
                CategoryOrder = categoryOrder,
                ProductOrder = productOrder
            };
        }
    }
}

/// <summary>
/// Result of Talabat sync operation
/// </summary>
public class TalabatSyncResult
{
    public string CorrelationId { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ImportId { get; set; }
    public string? Message { get; set; }
    public int CategoriesCount { get; set; }
    public int ProductsCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<string>? Errors { get; set; }

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

