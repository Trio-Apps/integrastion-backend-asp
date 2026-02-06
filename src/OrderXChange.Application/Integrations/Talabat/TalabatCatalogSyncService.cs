using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly TalabatSyncStatusService _syncStatusService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatCatalogSyncService> _logger;

    public TalabatCatalogSyncService(
        TalabatCatalogClient talabatCatalogClient,
        FoodicsToTalabatMapper mapper,
        TalabatSyncStatusService syncStatusService,
        IConfiguration configuration,
        ILogger<TalabatCatalogSyncService> logger)
    {
        _talabatCatalogClient = talabatCatalogClient;
        _mapper = mapper;
        _syncStatusService = syncStatusService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Sync Foodics products to Talabat catalog
    /// </summary>
    /// <param name="products">Foodics products with full includes</param>
    /// <param name="chainCode">Talabat chain code (e.g., "tlbt-pick")</param>
    /// <param name="vendorCode">Talabat vendor code</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync result</returns>
    public async Task<TalabatSyncResult> SyncCatalogAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        string chainCode,
        string vendorCode,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= Guid.NewGuid().ToString();
        var productsList = products.ToList();

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
            var catalogRequest = _mapper.MapToTalabatV2Catalog(productsList, chainCode, vendorCode, callbackUrl);

            var items = catalogRequest.Catalog?.Items;
            result.CategoriesCount = items?.Values.Count(i => string.Equals(i.Type, "Category", StringComparison.OrdinalIgnoreCase)) ?? 0;
            result.ProductsCount = items?.Values.Count(i => string.Equals(i.Type, "Product", StringComparison.OrdinalIgnoreCase)) ?? 0;

            _logger.LogInformation(
                "Mapped V2 catalog for Talabat. CorrelationId={CorrelationId}, ChainCode={ChainCode}, Items={Items}, Categories={Categories}, Products={Products}",
                correlationId,
                chainCode,
                items?.Count ?? 0,
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
        string? correlationId = null,
        string? vendorCode = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= Guid.NewGuid().ToString();
        var productsList = products.ToList();

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
            var catalogRequest = _mapper.MapToTalabatV2Catalog(productsList, chainCode, vendorCode: vendorCode, callbackUrl: callbackUrl);

            // V2 format uses Items dictionary where items can be of various types (Product, Category, Topping, etc.)
            result.CategoriesCount = catalogRequest.Catalog?.Items?.Count(x => x.Value.Type == "Category") ?? 0;
            result.ProductsCount = catalogRequest.Catalog?.Items?.Count(x => x.Value.Type == "Product") ?? 0;

            _logger.LogInformation(
                "Mapped catalog to Talabat V2 format. CorrelationId={CorrelationId}, Categories={Categories}, TotalItems={Items}, Products={Products}",
                correlationId,
                result.CategoriesCount,
                catalogRequest.Catalog?.Items?.Count ?? 0,
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

