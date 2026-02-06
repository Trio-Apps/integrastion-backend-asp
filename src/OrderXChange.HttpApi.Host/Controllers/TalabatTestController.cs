using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Application.Staging;
using OrderXChange.Domain.Staging;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.HttpApi.Host.Controllers;

/// <summary>
/// Test controller for verifying Talabat integration
/// Use this during development to test API connectivity
/// </summary>
[Route("api/talabat/test")]
[AllowAnonymous]
public class TalabatTestController : AbpController
{
    private readonly TalabatAuthClient _authClient;
    private readonly TalabatCatalogClient _catalogClient;
    private readonly TalabatCatalogSyncService _syncService;
    private readonly FoodicsToTalabatMapper _mapper;
    private readonly FoodicsCatalogClient _foodicsCatalogClient;
    private readonly FoodicsProductStagingService _stagingService;
    private readonly FoodicsProductStagingToFoodicsConverter _stagingConverter;
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatTestController> _logger;

    public TalabatTestController(
        TalabatAuthClient authClient,
        TalabatCatalogClient catalogClient,
        TalabatCatalogSyncService syncService,
        FoodicsToTalabatMapper mapper,
        FoodicsCatalogClient foodicsCatalogClient,
        FoodicsProductStagingService stagingService,
        FoodicsProductStagingToFoodicsConverter stagingConverter,
        IRepository<FoodicsProductStaging, Guid> stagingRepository,
        IConfiguration configuration,
        ILogger<TalabatTestController> logger)
    {
        _authClient = authClient;
        _catalogClient = catalogClient;
        _syncService = syncService;
        _mapper = mapper;
        _foodicsCatalogClient = foodicsCatalogClient;
        _stagingService = stagingService;
        _stagingConverter = stagingConverter;
        _stagingRepository = stagingRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Test Talabat login/authentication
    /// GET /api/talabat/test/login
    /// </summary>
    [HttpGet("login")]
    public async Task<IActionResult> TestLoginAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Testing Talabat login...");
            
            var token = await _authClient.GetAccessTokenAsync(null, cancellationToken);
            
            return Ok(new
            {
                success = true,
                message = "Talabat login successful!",
                tokenPreview = token.Length > 20 ? $"{token[..20]}..." : token,
                tokenLength = token.Length,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Talabat login test failed");
            return Ok(new
            {
                success = false,
                message = "Talabat login failed",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// One-shot flow: Foodics → Staging → Talabat (bypasses Kafka)
    /// POST /api/talabat/test/trigger-full
    /// </summary>
    [HttpPost("trigger-full")]
    public async Task<IActionResult> TriggerFullAsync(
        [FromQuery] Guid? foodicsAccountId = null,
        [FromQuery] string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var testId = Guid.NewGuid().ToString("N")[..8];
        var accountId = foodicsAccountId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        var chainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";

        _logger.LogInformation("⚡ [Full {TestId}] Starting Foodics → Staging → Talabat. Account={AccountId}, Branch={Branch}, ChainCode={ChainCode}",
            testId, accountId, branchId ?? "<all>", chainCode);

        try
        {
            // 1) Fetch from Foodics
            _logger.LogInformation("⚡ [Full {TestId}] Fetching products from Foodics...", testId);
            var products = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId,
                perPage: 100,
                includeInactive: false,
                cancellationToken: cancellationToken);

            _logger.LogInformation("⚡ [Full {TestId}] Fetched {Count} products from Foodics", testId, products.Count);

            if (!products.Any())
            {
                return Ok(new
                {
                    success = false,
                    message = "No products returned from Foodics",
                    testId,
                    timestamp = DateTime.UtcNow
                });
            }

            // 2) Save to staging
            _logger.LogInformation("⚡ [Full {TestId}] Saving products to staging...", testId);
            var stagingResult = await _stagingService.SaveProductsToStagingAsync(
                accountId,
                products.Values,
                branchId,
                cancellationToken);

            _logger.LogInformation(
                "⚡ [Full {TestId}] Staging save result: Saved={Saved}, Updated={Updated}, Errors={Errors}, Total={Total}",
                testId,
                stagingResult.SavedCount,
                stagingResult.UpdatedCount,
                stagingResult.ErrorCount,
                stagingResult.TotalProcessed);

            // 3) Load staging and convert to Foodics DTOs
            var stagingProducts = await _stagingRepository.GetListAsync(
                x => x.FoodicsAccountId == accountId && x.IsActive,
                cancellationToken: cancellationToken);

            if (!stagingProducts.Any())
            {
                return Ok(new
                {
                    success = false,
                    message = "No active products in staging after sync",
                    testId,
                    timestamp = DateTime.UtcNow
                });
            }

            var foodicsDtos = _stagingConverter.ConvertToFoodicsDto(stagingProducts);

            // 4) Submit to Talabat (V2)
            _logger.LogInformation("⚡ [Full {TestId}] Submitting to Talabat...", testId);
            var correlationId = $"full-{testId}";
            var submitResult = await _syncService.SyncCatalogV2Async(
                foodicsDtos,
                chainCode,
                accountId,
                branchId,
                correlationId,
                vendorCode: null,
                cancellationToken);

            if (submitResult.Success)
            {
                _logger.LogInformation("✅ [Full {TestId}] Talabat submission SUCCESS. ImportId={ImportId}", testId, submitResult.ImportId);
                return Ok(new
                {
                    success = true,
                    message = "Full flow completed successfully",
                    testId,
                    correlationId,
                    importId = submitResult.ImportId,
                    categoriesCount = submitResult.CategoriesCount,
                    productsCount = submitResult.ProductsCount,
                    stagingSaved = stagingResult.SavedCount,
                    stagingUpdated = stagingResult.UpdatedCount,
                    stagingErrors = stagingResult.ErrorCount,
                    stagingTotal = stagingResult.TotalProcessed,
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("❌ [Full {TestId}] Talabat submission FAILED. Message={Message}", testId, submitResult.Message);
                return Ok(new
                {
                    success = false,
                    message = submitResult.Message ?? "Talabat submission failed",
                    testId,
                    correlationId,
                    errors = submitResult.Errors,
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 [Full {TestId}] Error in full flow", testId);
            return Ok(new
            {
                success = false,
                message = ex.Message,
                testId,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Test full flow: Fetch from Foodics and push to Talabat
    /// V2 API: PUT /v2/chains/{chainCode}/catalog
    /// GET /api/talabat/test/sync
    /// </summary>
    [HttpGet("sync")]
    public async Task<IActionResult> TestSyncAsync(
        [FromQuery] string? chainCode = null,
        [FromQuery] string? branchId = null,
        [FromQuery] Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        // V2 API uses chainCode instead of vendorCode
        chainCode ??= _configuration["Talabat:ChainCode"] ?? _configuration["Talabat:DefaultVendorCode"] ?? "783216";
        var platformVendorId = _configuration["Talabat:PlatformVendorId"] ?? "PH-SIDDIQ-002";
        var accountId = foodicsAccountId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        
        try
        {
            _logger.LogInformation("Testing Talabat V2 sync. ChainCode={ChainCode}, PlatformVendorId={PlatformVendorId}, Branch={Branch}", 
                chainCode, platformVendorId, branchId ?? "<all>");

            // Step 1: Fetch products from Foodics
            _logger.LogInformation("Step 1: Fetching products from Foodics...");
            var products = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId,
                perPage: 50, // Limit for testing
                includeInactive: false,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Fetched {Count} products from Foodics", products.Count);

            if (products.Count == 0)
            {
                return Ok(new
                {
                    success = false,
                    message = "No products found in Foodics",
                    chainCode,
                    platformVendorId,
                    timestamp = DateTime.UtcNow
                });
            }

            // Step 2: Map to Talabat format (includes vendors array for V2 API)
            _logger.LogInformation("Step 2: Mapping products to Talabat V2 format...");
            var catalogRequest = _mapper.MapToTalabatCatalog(products.Values, platformVendorId);

            // Step 3: Push to Talabat V2 API
            _logger.LogInformation("Step 3: Pushing catalog to Talabat V2 API...");
            var result = await _syncService.SyncCatalogAsync(
                products.Values,
                chainCode,
                accountId,
                branchId,
                platformVendorId,
                correlationId: Guid.NewGuid().ToString(),
                cancellationToken);

            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                chainCode,
                platformVendorId,
                importId = result.ImportId,
                stats = new
                {
                    foodicsProducts = products.Count,
                    talabatCategories = result.CategoriesCount,
                    talabatProducts = result.ProductsCount,
                    durationMs = result.Duration?.TotalMilliseconds
                },
                errors = result.Errors,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Talabat V2 sync test failed");
            return Ok(new
            {
                success = false,
                message = "Talabat sync test failed",
                error = ex.Message,
                chainCode,
                platformVendorId,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Preview the mapped catalog (without sending to Talabat)
    /// GET /api/talabat/test/preview
    /// </summary>
    [HttpGet("preview")]
    public async Task<IActionResult> PreviewCatalogAsync(
        [FromQuery] string? vendorCode = null,
        [FromQuery] string? branchId = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        vendorCode ??= _configuration["Talabat:DefaultVendorCode"] ?? "test-vendor-dev-001";

        try
        {
            // Fetch products from Foodics
            var products = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId,
                perPage: limit,
                includeInactive: false,
                cancellationToken: cancellationToken);

            if (products.Count == 0)
            {
                return Ok(new
                {
                    success = false,
                    message = "No products found in Foodics",
                    timestamp = DateTime.UtcNow
                });
            }

            // Map to Talabat format
            var catalogRequest = _mapper.MapToTalabatCatalog(products.Values, vendorCode);

            return Ok(new
            {
                success = true,
                vendorCode,
                stats = new
                {
                    foodicsProducts = products.Count,
                    categories = catalogRequest.Menu.Categories.Count,
                    totalProducts = catalogRequest.Menu.Categories.Sum(c => c.Products.Count)
                },
                preview = catalogRequest,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog preview failed");
            return Ok(new
            {
                success = false,
                message = "Failed to preview catalog",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Test V2 format: Fetch from Foodics and generate V2 catalog format
    /// GET /api/talabat/test/sync-v2
    /// Returns the V2 catalog structure that matches Talabat's example format
    /// First performs login to get JWT token, then uses it for catalog submission
    /// </summary>
    [HttpGet("sync-v2")]
    public async Task<IActionResult> TestSyncV2Async(
        [FromQuery] string? chainCode = null,
        [FromQuery] string? branchId = null,
        [FromQuery] bool submit = false,
        CancellationToken cancellationToken = default)
    {
        chainCode ??= _configuration["Talabat:ChainCode"] ?? "tlbt-pick";
        var platformVendorId = _configuration["Talabat:PlatformVendorId"] ?? "PH-SIDDIQ-002";
        
        try
        {
            _logger.LogInformation(
                "Testing Talabat V2 Items-based format. ChainCode={ChainCode}, BranchId={BranchId}, Submit={Submit}", 
                chainCode, branchId ?? "<all>", submit);

            // Step 0: Login to Talabat and get JWT token
            // COMMENTED OUT: Using static token from configuration instead
            string? accessToken = null;
            
            try
            {
                _logger.LogInformation("Step 0: Logging in to Talabat to get JWT token...");
                accessToken = await _authClient.GetAccessTokenAsync(null, cancellationToken);
                _logger.LogInformation(
                    "Talabat login successful. Token length={TokenLength}, Token preview={TokenPreview}",
                    accessToken.Length,
                    accessToken.Length > 30 ? $"{accessToken[..30]}..." : accessToken);
            }
            catch (Exception loginEx)
            {
                _logger.LogError(loginEx, "Talabat login failed");
                return Ok(new
                {
                    success = false,
                    message = "Failed to login to Talabat",
                    error = loginEx.Message,
                    errorType = loginEx.GetType().Name,
                    chainCode,
                    branchId,
                    timestamp = DateTime.UtcNow
                });
            }
            

            // Using static token from configuration
            //try
            //{
            //    _logger.LogInformation("Step 0: Using static token from configuration...");
            //    accessToken = await _authClient.GetAccessTokenAsync(cancellationToken);
            //    _logger.LogInformation(
            //        "Static token retrieved. Token length={TokenLength}, Token preview={TokenPreview}",
            //        accessToken?.Length ?? 0,
            //        accessToken != null && accessToken.Length > 30 ? $"{accessToken[..30]}..." : accessToken);
            //}
            //catch (Exception tokenEx)
            //{
            //    _logger.LogError(tokenEx, "Failed to get access token");
            //    return Ok(new
            //    {
            //        success = false,
            //        message = "Failed to get access token",
            //        error = tokenEx.Message,
            //        errorType = tokenEx.GetType().Name,
            //        chainCode,
            //        branchId,
            //        timestamp = DateTime.UtcNow
            //    });
            //}

            // Step 1: Fetch products from Foodics
            _logger.LogInformation("Step 1: Fetching products from Foodics...");
            var products = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId,
                perPage: 50,
                includeInactive: false,
                cancellationToken: cancellationToken);

            if (products.Count == 0)
            {
                return Ok(new
                {
                    success = false,
                    message = "No products found in Foodics",
                    chainCode,
                    branchId,
                    tokenObtained = !string.IsNullOrEmpty(accessToken),
                    tokenPreview = accessToken != null && accessToken.Length > 30 ? $"{accessToken[..30]}..." : accessToken,
                    timestamp = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Fetched {Count} products from Foodics", products.Count);

            // Analyze Foodics data quality
            // Note: Foodics returns category as object (Category.Id) not as separate CategoryId field
            var productsWithCategory = products.Values.Count(p => 
                !string.IsNullOrWhiteSpace(p.Category?.Id) || !string.IsNullOrWhiteSpace(p.CategoryId));
            var productsWithModifiers = products.Values.Count(p => p.Modifiers != null && p.Modifiers.Count > 0);
            var productsWithImages = products.Values.Count(p => !string.IsNullOrWhiteSpace(p.Image));
            
            _logger.LogInformation(
                "📊 Foodics data analysis: Total={Total}, WithCategory={WithCategory}, WithModifiers={WithModifiers}, WithImages={WithImages}",
                products.Count,
                productsWithCategory,
                productsWithModifiers,
                productsWithImages);

            if (productsWithCategory == 0)
            {
                _logger.LogWarning("⚠️ WARNING: No products have Category set in Foodics! Categories will not be created.");
            }

            // Step 2: Map to V2 format (items-based)
            _logger.LogInformation("Step 2: Mapping products to Talabat V2 format...");
            var v2CatalogRequest = _mapper.MapToTalabatV2Catalog(products.Values, chainCode);

            // Validate catalog structure before submission
            var validationErrors = ValidateV2Catalog(v2CatalogRequest);
            if (validationErrors.Count > 0)
            {
                _logger.LogWarning(
                    "V2 catalog validation failed. ChainCode={ChainCode}, Errors={Errors}",
                    chainCode,
                    string.Join("; ", validationErrors));

                return Ok(new
                {
                    success = false,
                    message = "Catalog validation failed",
                    chainCode,
                    branchId,
                    validationErrors,
                    tokenObtained = !string.IsNullOrEmpty(accessToken),
                    tokenPreview = accessToken != null && accessToken.Length > 30 ? $"{accessToken[..30]}..." : accessToken,
                    catalog = v2CatalogRequest, // Still return catalog for debugging
                    timestamp = DateTime.UtcNow
                });
            }

            // Step 3: Always submit to Talabat using the obtained token
            _logger.LogInformation("Step 3: Submitting catalog to Talabat using JWT token...");
            try
            {
                var response = await _catalogClient.SubmitV2CatalogAsync(
                    chainCode,
                    v2CatalogRequest,
                    vendorCode: null,
                    cancellationToken);

                // Log success or failure clearly
                if (response.Success)
                {
                    _logger.LogInformation(
                        "✅ SUCCESS: Catalog submitted to Talabat! ImportId={ImportId}, ChainCode={ChainCode}, Vendors={Vendors}",
                        response.ImportId,
                        chainCode,
                        string.Join(", ", v2CatalogRequest.Vendors));
                }
                else
                {
                    var errorMessages = response.Errors?.Select(e => e.Message ?? e.Code ?? "Unknown error").ToList() 
                        ?? new List<string>();
                    _logger.LogWarning(
                        "⚠️ FAILED: Talabat rejected catalog. ChainCode={ChainCode}, Message={Message}, Errors={Errors}",
                        chainCode,
                        response.Message,
                        string.Join("; ", errorMessages));
                }

                return Ok(new
                {
                    success = response.Success,
                    message = response.Success 
                        ? $"✅ Catalog submitted successfully! ImportId: {response.ImportId ?? "will be provided via callback"}" 
                        : $"❌ Failed: {response.Message}",
                    chainCode,
                    branchId,
                    vendors = v2CatalogRequest.Vendors,
                    importId = response.ImportId,
                    tokenObtained = !string.IsNullOrEmpty(accessToken),
                    tokenPreview = accessToken != null && accessToken.Length > 30 ? $"{accessToken[..30]}..." : accessToken,
                    foodicsDataQuality = new
                    {
                        totalProducts = products.Count,
                        withCategory = productsWithCategory,
                        withModifiers = productsWithModifiers,
                        withImages = productsWithImages,
                        warning = productsWithCategory == 0 
                            ? "⚠️ No products have CategoryId in Foodics! Assign categories in Foodics to create Talabat categories." 
                            : null
                    },
                    catalogStats = new
                    {
                        totalItems = v2CatalogRequest.Catalog?.Items?.Count ?? 0,
                        categories = v2CatalogRequest.Catalog?.Items?.Values.Count(i => i.Type == "Category") ?? 0,
                        products = v2CatalogRequest.Catalog?.Items?.Values.Count(i => i.Type == "Product") ?? 0,
                        toppings = v2CatalogRequest.Catalog?.Items?.Values.Count(i => i.Type == "Topping") ?? 0,
                        images = v2CatalogRequest.Catalog?.Items?.Values.Count(i => i.Type == "Image") ?? 0,
                        schedules = v2CatalogRequest.Catalog?.Items?.Values.Count(i => i.Type == "ScheduleEntry") ?? 0,
                        menus = v2CatalogRequest.Catalog?.Items?.Values.Count(i => i.Type == "Menu") ?? 0,
                        hiddenCategory = v2CatalogRequest.Catalog?.Items?.ContainsKey("Category#hiddenId") ?? false
                    },
                    errors = response.Errors,
                    curlCommand = GenerateCurlCommand(chainCode, v2CatalogRequest, accessToken),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                // Log full error details for debugging
                _logger.LogError(
                    httpEx,
                    "❌ HTTP ERROR submitting V2 catalog. ChainCode={ChainCode}, Message={Message}",
                    chainCode,
                    httpEx.Message);

                // Try to extract error details from inner exception or data
                var errorDetails = httpEx.Message;
                if (httpEx.Data.Contains("ResponseBody"))
                {
                    errorDetails = httpEx.Data["ResponseBody"]?.ToString() ?? errorDetails;
                }

                return Ok(new
                {
                    success = false,
                    message = $"❌ HTTP Error: {errorDetails}",
                    chainCode,
                    branchId,
                    vendors = v2CatalogRequest.Vendors,
                    error = errorDetails,
                    errorType = "HttpRequestException",
                    tokenObtained = !string.IsNullOrEmpty(accessToken),
                    tokenPreview = accessToken != null && accessToken.Length > 30 ? $"{accessToken[..30]}..." : accessToken,
                    stats = new
                    {
                        catalogItems = v2CatalogRequest.Catalog?.Items?.Count ?? 0,
                        categories = v2CatalogRequest.Catalog?.Items?.Values.Count(i => i.Type == "Category") ?? 0,
                        products = v2CatalogRequest.Catalog?.Items?.Values.Count(i => i.Type == "Product") ?? 0,
                        hiddenCategory = v2CatalogRequest.Catalog?.Items?.ContainsKey("Category#hiddenId") ?? false
                    },
                    curlCommand = GenerateCurlCommand(chainCode, v2CatalogRequest, accessToken),
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Talabat V2 sync test failed");
            return Ok(new
            {
                success = false,
                message = "Talabat V2 sync test failed",
                error = ex.Message,
                errorType = ex.GetType().Name,
                chainCode,
                branchId,
                stackTrace = ex.StackTrace, // Include stack trace for debugging
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Validates V2 catalog structure before submission
    /// </summary>
    private List<string> ValidateV2Catalog(TalabatV2CatalogSubmitRequest request)
    {
        var errors = new List<string>();

        if (request.Catalog?.Items == null || request.Catalog.Items.Count == 0)
        {
            errors.Add("Catalog items dictionary is empty");
            return errors;
        }

        if (request.Vendors == null || request.Vendors.Count == 0)
        {
            errors.Add("Vendors array is required and cannot be empty");
        }

        // Validate all item IDs are unique
        var itemIds = request.Catalog.Items.Keys.ToList();
        var duplicateIds = itemIds.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        
        if (duplicateIds.Any())
        {
            errors.Add($"Duplicate item IDs found: {string.Join(", ", duplicateIds)}");
        }

        // Validate references point to existing items
        foreach (var item in request.Catalog.Items.Values)
        {
            // Validate product references in categories
            if (item.Products != null)
            {
                foreach (var productRef in item.Products.Keys)
                {
                    if (!request.Catalog.Items.ContainsKey(productRef))
                    {
                        errors.Add($"Category '{item.Id}' references non-existent product '{productRef}'");
                    }
                }
            }

            // Validate topping references in products
            if (item.Toppings != null)
            {
                foreach (var toppingRef in item.Toppings.Keys)
                {
                    if (!request.Catalog.Items.ContainsKey(toppingRef))
                    {
                        errors.Add($"Product '{item.Id}' references non-existent topping '{toppingRef}'");
                    }
                }
            }

            // Validate image references in products
            if (item.Images != null)
            {
                foreach (var imageRef in item.Images.Keys)
                {
                    if (!request.Catalog.Items.ContainsKey(imageRef))
                    {
                        errors.Add($"Product '{item.Id}' references non-existent image '{imageRef}'");
                    }
                }
            }

            // Validate variant references
            if (item.Variants != null)
            {
                foreach (var variantRef in item.Variants.Keys)
                {
                    if (!request.Catalog.Items.ContainsKey(variantRef))
                    {
                        errors.Add($"Product '{item.Id}' references non-existent variant '{variantRef}'");
                    }
                }
            }

            // Validate topping product references
            if (item.Type == "Topping" && item.Products != null)
            {
                foreach (var productRef in item.Products.Keys)
                {
                    if (!request.Catalog.Items.ContainsKey(productRef))
                    {
                        errors.Add($"Topping '{item.Id}' references non-existent product '{productRef}'");
                    }
                }
            }
        }

        // Validate Menu exists
        var menuItems = request.Catalog.Items.Values.Where(i => i.Type == "Menu").ToList();
        if (menuItems.Count == 0)
        {
            errors.Add("At least one Menu item is required");
        }

        return errors;
    }

    /// <summary>
    /// Generate curl command for V2 catalog submission
    /// </summary>
    private string GenerateCurlCommand(string chainCode, TalabatV2CatalogSubmitRequest request, string? accessToken = null)
    {
        var baseUrl = _configuration["Talabat:BaseUrl"] ?? "https://integration-middleware.me.restaurant-partners.com";
        var url = $"{baseUrl}/v2/chains/{chainCode}/catalog";
        
        // Serialize request to JSON
        var json = System.Text.Json.JsonSerializer.Serialize(request, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        // Escape JSON for curl command
        var escapedJson = json.Replace("'", "'\\''");

        // Use actual token if provided, otherwise use placeholder
        var tokenValue = !string.IsNullOrEmpty(accessToken) ? accessToken : "YOUR_JWT_TOKEN";

        return $"curl --location --request PUT '{url}' \\\n" +
               $"--header 'Content-Type: application/json' \\\n" +
               $"--header 'Authorization: Bearer {tokenValue}' \\\n" +
               $"--data '{escapedJson}'";
    }

    /// <summary>
    /// Get current Talabat configuration (masked)
    /// GET /api/talabat/test/config
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var baseUrl = _configuration["Talabat:BaseUrl"];
        var username = _configuration["Talabat:Username"];
        var chainCode = _configuration["Talabat:ChainCode"];
        var vendorCode = _configuration["Talabat:DefaultVendorCode"];
        var platformVendorId = _configuration["Talabat:PlatformVendorId"];
        var enabled = _configuration.GetValue<bool>("Talabat:Enabled", true);

        return Ok(new
        {
            apiVersion = "V2",
            baseUrl,
            username,
            chainCode,
            defaultVendorCode = vendorCode,
            platformVendorId,
            enabled,
            hasPassword = !string.IsNullOrEmpty(_configuration["Talabat:Password"]),
            hasSecret = !string.IsNullOrEmpty(_configuration["Talabat:Secret"]),
            callbackUrl = _configuration["Talabat:CallbackBaseUrl"],
            catalogEndpoint = $"{baseUrl}/v2/chains/{chainCode}/catalog",
            webhookEndpoints = new[]
            {
                "/api/talabat/webhooks/catalog-status",
                "/api/talabat/webhooks/menu-request",
                "/api/talabat/catalog-status (legacy)"
            },
            timestamp = DateTime.UtcNow
        });
    }

    #region Branch-Specific Availability Endpoints

    /// <summary>
    /// Update item availability for a specific branch
    /// POST /api/talabat/test/branch/{vendorCode}/availability
    /// </summary>
    [HttpPost("branch/{vendorCode}/availability")]
    public async Task<IActionResult> UpdateBranchItemAvailability(
        string vendorCode,
        [FromBody] TalabatBranchItemAvailabilityRequest request)
    {
        _logger.LogInformation(
            "🏪 Test: Updating branch item availability. VendorCode={VendorCode}, ItemCount={ItemCount}",
            vendorCode,
            request.Items?.Count ?? 0);

        try
        {
            var response = await _catalogClient.UpdateBranchItemAvailabilityAsync(
                vendorCode,
                request);

            return Ok(new
            {
                success = response.Success,
                message = response.Message,
                vendorCode = response.VendorCode,
                updatedCount = response.UpdatedCount,
                failedCount = response.FailedCount,
                errors = response.Errors,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating branch item availability");
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                vendorCode,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Update item availability across multiple branches
    /// POST /api/talabat/test/branches/availability
    /// </summary>
    [HttpPost("branches/availability")]
    public async Task<IActionResult> UpdateMultiBranchAvailability(
        [FromBody] TalabatMultiBranchAvailabilityRequest request)
    {
        _logger.LogInformation(
            "🏢 Test: Updating multi-branch availability. BranchCount={BranchCount}",
            request.Branches?.Count ?? 0);

        try
        {
            var response = await _catalogClient.UpdateMultiBranchAvailabilityAsync(request);

            return Ok(new
            {
                success = response.Success,
                message = response.Message,
                results = response.Results,
                totalBranches = response.Results?.Count ?? 0,
                successfulBranches = response.Results?.Count(r => r.Success) ?? 0,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating multi-branch availability");
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Toggle single item availability for a branch
    /// POST /api/talabat/test/branch/{vendorCode}/item/{remoteCode}/toggle
    /// </summary>
    [HttpPost("branch/{vendorCode}/item/{remoteCode}/toggle")]
    public async Task<IActionResult> ToggleItemAvailability(
        string vendorCode,
        string remoteCode,
        [FromQuery] bool available = true,
        [FromQuery] string? reason = null)
    {
        _logger.LogInformation(
            "🔄 Test: Toggling item availability. VendorCode={VendorCode}, RemoteCode={RemoteCode}, Available={Available}",
            vendorCode,
            remoteCode,
            available);

        try
        {
            var request = new TalabatBranchItemAvailabilityRequest
            {
                Items = new List<TalabatBranchItemAvailability>
                {
                    new TalabatBranchItemAvailability
                    {
                        RemoteCode = remoteCode,
                        IsAvailable = available,
                        Reason = reason
                    }
                }
            };

            var response = await _catalogClient.UpdateBranchItemAvailabilityAsync(
                vendorCode,
                request);

            return Ok(new
            {
                success = response.Success,
                message = response.Message,
                vendorCode,
                remoteCode,
                isAvailable = available,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling item availability");
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get catalog import status from Talabat
    /// GET /api/talabat/test/import-status/{chainCode}
    /// </summary>
    [HttpGet("import-status/{chainCode?}")]
    public async Task<IActionResult> GetImportStatus(string? chainCode = null)
    {
        chainCode ??= _configuration["Talabat:ChainCode"] ?? "default";

        _logger.LogInformation("📋 Getting catalog import status. ChainCode={ChainCode}", chainCode);

        try
        {
            var status = await _catalogClient.GetCatalogImportLogAsync(chainCode);

            if (status == null)
            {
                return Ok(new
                {
                    success = true,
                    message = "No import log found or API not available",
                    chainCode,
                    timestamp = DateTime.UtcNow
                });
            }

            return Ok(new
            {
                success = true,
                chainCode,
                importId = status.ImportId,
                status = status.Status,
                createdAt = status.CreatedAt,
                completedAt = status.CompletedAt,
                summary = status.Summary,
                errors = status.Errors,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting import status");
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                chainCode,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// DELETE all items from Talabat by submitting an empty catalog
    /// WARNING: This will remove ALL menu items from Talabat!
    /// DELETE /api/talabat/test/clear-catalog
    /// </summary>
    [HttpDelete("clear-catalog")]
    public async Task<IActionResult> ClearCatalogAsync(
        [FromQuery] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        if (!confirm)
        {
            return BadRequest(new
            {
                success = false,
                message = "⚠️ WARNING: This will DELETE ALL items from Talabat! Add ?confirm=true to proceed.",
                hint = "Use: DELETE /api/talabat/test/clear-catalog?confirm=true",
                timestamp = DateTime.UtcNow
            });
        }

        try
        {
            var chainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";
            var vendorCodes = _configuration.GetSection("Talabat:Vendors").Get<string[]>() 
                ?? new[] { _configuration["Talabat:DefaultVendorCode"] ?? "PH-SIDDIQ-002" };

            _logger.LogWarning(
                "🗑️ CLEARING ALL ITEMS from Talabat! ChainCode={ChainCode}, Vendors={Vendors}",
                chainCode,
                string.Join(", ", vendorCodes));

            // Create catalog with empty menu (Menu item required for valid catalog)
            var menuId = "menu-clear-all";
            var scheduleId = "schedule-clear-all";
            var emptyRequest = new TalabatV2CatalogSubmitRequest    
            {
                Catalog = new TalabatV2Catalog
                {
                    Items = new Dictionary<string, TalabatV2CatalogItem>
                    {
                        // Menu item with no products = clears all products
                        [menuId] = new TalabatV2CatalogItem
                        {
                            Id = menuId,
                            Type = "Menu",
                            Title = new TalabatV2Title { Default = "Empty Menu" },
                            MenuType = "DELIVERY",
                            Schedule = new Dictionary<string, TalabatV2ItemReference>
                            {
                                [scheduleId] = new TalabatV2ItemReference { Id = scheduleId }
                            }
                        },
                        // Schedule entry for the menu
                        [scheduleId] = new TalabatV2CatalogItem
                        {
                            Id = scheduleId,
                            Type = "ScheduleEntry",
                            StartTime = "00:00",
                            EndTime = "23:59",
                            WeekDays = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }
                        }
                    }
                },
                Vendors = vendorCodes.ToList(),
                CallbackUrl = null // No callback needed for delete
            };

            // Submit empty catalog
            var response = await _catalogClient.SubmitV2CatalogAsync(chainCode, emptyRequest, vendorCode: null, cancellationToken);

            _logger.LogWarning(
                "🗑️ Empty catalog submitted to Talabat. ChainCode={ChainCode}, ImportId={ImportId}",
                chainCode,
                response.ImportId);

            return Ok(new
            {
                success = response.Success,
                message = "🗑️ Empty catalog submitted - all items will be removed from Talabat",
                chainCode,
                vendors = vendorCodes,
                importId = response.ImportId,
                apiMessage = response.Message,
                warning = "Items may take a few minutes to be removed from Talabat",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Talabat catalog");
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Delete specific items from Talabat by submitting catalog without those items
    /// POST /api/talabat/test/delete-items
    /// </summary>
    [HttpPost("delete-items")]
    public async Task<IActionResult> DeleteSpecificItemsAsync(
        [FromBody] DeleteItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.RemoteCodesToDelete == null || !request.RemoteCodesToDelete.Any())
        {
            return BadRequest(new
            {
                success = false,
                message = "Please provide remoteCodesToDelete array",
                example = new { remoteCodesToDelete = new[] { "product-id-1", "product-id-2" } }
            });
        }

        try
        {
            var chainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";
            
            _logger.LogInformation(
                "Fetching current products from Foodics to rebuild catalog without deleted items...");

            // Get current products from Foodics
            var products = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId: null, 
                accessToken: null, 
                perPage: 100,
                includeInactive: false,
                cancellationToken: cancellationToken);
            
            // Prepare matching lists
            var productIdSet = products.Keys.ToHashSet();
            var matchedIds = request.RemoteCodesToDelete
                .Where(id => productIdSet.Contains(id))
                .Distinct()
                .ToList();

            var notFoundIds = request.RemoteCodesToDelete
                .Where(id => !productIdSet.Contains(id))
                .Distinct()
                .ToList();

            // Filter out the items to delete (only those that actually exist)
            var remainingProducts = products
                .Where(kvp => !matchedIds.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            _logger.LogInformation(
                "Rebuilding catalog. Original: {Original}, After deletion: {Remaining}, Deleted: {Deleted}",
                products.Count,
                remainingProducts.Count,
                request.RemoteCodesToDelete.Count);

            if (!matchedIds.Any())
            {
                return Ok(new
                {
                    success = false,
                    message = "None of the specified remoteCodes were found in the current catalog",
                    requestedCount = request.RemoteCodesToDelete.Count,
                    matchedCount = 0,
                    notFound = notFoundIds,
                    sampleAvailableIds = products.Keys.Take(10),
                    hint = "Use Foodics product IDs (field: id) from /products?include=category,..."
                });
            }

            // Map remaining products to Talabat format
            var catalogRequest = _mapper.MapToTalabatV2Catalog(remainingProducts.Values, chainCode, null);

            // Submit the new catalog (without the deleted items)
            var response = await _catalogClient.SubmitV2CatalogAsync(chainCode, catalogRequest, vendorCode: null, cancellationToken);

            return Ok(new
            {
                success = response.Success,
                message = $"Catalog submitted without {matchedIds.Count} items",
                deletedCount = matchedIds.Count,
                remainingCount = remainingProducts.Count,
                matchedIds,
                notFoundIds,
                importId = response.ImportId,
                apiMessage = response.Message,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting items from Talabat");
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Clear ALL products for a specific vendor by submitting an empty catalog
    /// POST /api/talabat/test/clear-vendor/{vendorCode}
    /// </summary>
    [HttpPost("clear-vendor/{vendorCode}")]
    public async Task<IActionResult> ClearVendorCatalogAsync(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";

            _logger.LogWarning(
                "🗑️ Submitting CLEAR catalog to Talabat for Vendor={VendorCode}, ChainCode={ChainCode}",
                vendorCode,
                chainCode);

            // Build minimal catalog with ONE INACTIVE dummy product
            // Talabat rejects completely empty catalogs - must have at least one item
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var dummyProductId = $"clear-dummy-{timestamp}";
            var dummyCategoryId = $"clear-cat-{timestamp}";
            var dummyMenuId = $"clear-menu-{timestamp}";
            var dummyScheduleId = $"clear-schedule-{timestamp}";

            var emptyRequest = new TalabatV2CatalogSubmitRequest
            {
                Catalog = new TalabatV2Catalog
                {
                    Items = new Dictionary<string, TalabatV2CatalogItem>
                    {
                        // Schedule (24/7)
                        [dummyScheduleId] = new TalabatV2CatalogItem
                        {
                            Id = dummyScheduleId,
                            Type = "ScheduleEntry",
                            StartTime = "00:00",
                            EndTime = "23:59",
                            WeekDays = new List<string> { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" }
                        },
                        // Menu - links to Category
                        [dummyMenuId] = new TalabatV2CatalogItem
                        {
                            Id = dummyMenuId,
                            Type = "Menu",
                            Title = new TalabatV2Title { Default = "Clearing Menu..." },
                            Schedule = new Dictionary<string, TalabatV2ItemReference>
                            {
                                [dummyScheduleId] = new TalabatV2ItemReference { Id = dummyScheduleId, Type = "ScheduleEntry" }
                            },
                            Products = new Dictionary<string, TalabatV2ItemReference>
                            {
                                [dummyCategoryId] = new TalabatV2ItemReference { Id = dummyCategoryId, Type = "Category", Order = 0 }
                            }
                        },
                        // Category - links to Product
                        [dummyCategoryId] = new TalabatV2CatalogItem
                        {
                            Id = dummyCategoryId,
                            Type = "Category",
                            Title = new TalabatV2Title { Default = "System" },
                            Order = 999,
                            Products = new Dictionary<string, TalabatV2ItemReference>
                            {
                                [dummyProductId] = new TalabatV2ItemReference { Id = dummyProductId, Type = "Product", Order = 0 }
                            }
                        },
                        // Product - INACTIVE (invisible to customers)
                        [dummyProductId] = new TalabatV2CatalogItem
                        {
                            Id = dummyProductId,
                            Type = "Product",
                            Title = new TalabatV2Title { Default = "Menu Clearing..." },
                            Description = new TalabatV2Title { Default = "Temporary item - menu is being updated" },
                            Price = "0.00",
                            Active = false
                        }
                    }
                },
                Vendors = new List<string> { vendorCode },
                CallbackUrl = _configuration["Talabat:CallbackBaseUrl"]?.TrimEnd('/') is string cb && !string.IsNullOrWhiteSpace(cb)
                    ? $"{cb}/catalog-status"
                    : null
            };

            var response = await _catalogClient.SubmitV2CatalogAsync(chainCode, emptyRequest, vendorCode: null, cancellationToken);

            return Ok(new
            {
                success = response.Success,
                message = $"🗑️ Empty catalog submitted. All items for vendor {vendorCode} will be removed.",
                chainCode,
                vendorCode,
                importId = response.ImportId,
                apiMessage = response.Message,
                timestamp = DateTime.UtcNow,
                callbackUrl = emptyRequest.CallbackUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Talabat catalog for vendor {VendorCode}", vendorCode);
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                vendorCode,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Hide all items from Talabat by marking them as unavailable
    /// This is an alternative to deleting - items remain but are not visible
    /// POST /api/talabat/test/hide-all-items
    /// </summary>
    [HttpPost("hide-all-items")]
    public async Task<IActionResult> HideAllItemsAsync(
        [FromQuery] string? vendorCode = null,
        [FromQuery] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        if (!confirm)
        {
            return BadRequest(new
            {
                success = false,
                message = "⚠️ WARNING: This will HIDE ALL items from Talabat menu! Add ?confirm=true to proceed.",
                hint = "Use: POST /api/talabat/test/hide-all-items?confirm=true",
                timestamp = DateTime.UtcNow
            });
        }

        try
        {
            vendorCode ??= _configuration["Talabat:DefaultVendorCode"] ?? "PH-SIDDIQ-002";
            
            _logger.LogWarning("🙈 Hiding ALL items for vendor {VendorCode}", vendorCode);

            // Get all products from Foodics
            var products = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId: null,
                accessToken: null,
                perPage: 100,
                includeInactive: false,
                cancellationToken: cancellationToken);

            if (products.Count == 0)
            {
                return Ok(new
                {
                    success = true,
                    message = "No products found to hide",
                    vendorCode,
                    timestamp = DateTime.UtcNow
                });
            }

            // Build availability update - mark all as unavailable
            var availabilityUpdates = products.Keys.Select(productId => new TalabatBranchItemAvailability
            {
                RemoteCode = productId,
                IsAvailable = false
            }).ToList();

            var request = new TalabatBranchItemAvailabilityRequest
            {
                Items = availabilityUpdates
            };

            // Update availability
            var response = await _catalogClient.UpdateBranchItemAvailabilityAsync(vendorCode, request, cancellationToken);

            return Ok(new
            {
                success = response.Success,
                message = $"🙈 {products.Count} items marked as UNAVAILABLE",
                vendorCode,
                itemsHidden = products.Count,
                apiMessage = response.Message,
                note = "Items are hidden from menu but still exist. Re-sync to restore.",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding items from Talabat");
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Show all items on Talabat by marking them as available
    /// POST /api/talabat/test/show-all-items
    /// </summary>
    [HttpPost("show-all-items")]
    public async Task<IActionResult> ShowAllItemsAsync(
        [FromQuery] string? vendorCode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            vendorCode ??= _configuration["Talabat:DefaultVendorCode"] ?? "PH-SIDDIQ-002";
            
            _logger.LogInformation("👁️ Showing ALL items for vendor {VendorCode}", vendorCode);

            // Get all products from Foodics
            var products = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId: null,
                accessToken: null,
                perPage: 100,
                includeInactive: false,
                cancellationToken: cancellationToken);

            if (products.Count == 0)
            {
                return Ok(new
                {
                    success = true,
                    message = "No products found to show",
                    vendorCode,
                    timestamp = DateTime.UtcNow
                });
            }

            // Build availability update - mark all as available
            var availabilityUpdates = products.Keys.Select(productId => new TalabatBranchItemAvailability
            {
                RemoteCode = productId,
                IsAvailable = true
            }).ToList();

            var request = new TalabatBranchItemAvailabilityRequest
            {
                Items = availabilityUpdates
            };

            // Update availability
            var response = await _catalogClient.UpdateBranchItemAvailabilityAsync(vendorCode, request, cancellationToken);

            return Ok(new
            {
                success = response.Success,
                message = $"👁️ {products.Count} items marked as AVAILABLE",
                vendorCode,
                itemsShown = products.Count,
                apiMessage = response.Message,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing items on Talabat");
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    #endregion
}

/// <summary>
/// Request model for deleting specific items
/// </summary>
public class DeleteItemsRequest
{
    /// <summary>
    /// List of remoteCode (Foodics product IDs) to delete from Talabat
    /// </summary>
    public List<string> RemoteCodesToDelete { get; set; } = new();
}

