using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodicsCatalogClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly string? _defaultAccessToken;
    private readonly JsonSerializerOptions _jsonOptions;

    public FoodicsCatalogClient(HttpClient httpClient, IConfiguration configuration, ILogger<FoodicsCatalogClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        var baseUrl = configuration["Foodics:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Foodics:BaseUrl configuration is missing.");
        }

        _httpClient.BaseAddress = new Uri(EnsureEndsWithSlash(baseUrl));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Set timeout to 2 minutes to handle large responses
        _httpClient.Timeout = TimeSpan.FromMinutes(2);

        // Store default token from configuration as fallback (backward compatibility)
        _defaultAccessToken = configuration["Foodics:ApiToken"] ?? configuration["Foodics:AccessToken"];

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Gets access token - uses provided token, falls back to configuration token
    /// </summary>
    private string GetAccessToken(string? accessToken = null)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return accessToken;
        }

        if (!string.IsNullOrWhiteSpace(_defaultAccessToken))
        {
            return _defaultAccessToken;
        }

        throw new InvalidOperationException(
            "Foodics access token is required. Provide accessToken parameter or configure Foodics:ApiToken/AccessToken in appsettings.");
    }

    public async Task<Dictionary<string, FoodicsCategoryInfoDto>> GetCategoriesByIdsAsync(
        IEnumerable<string> ids,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        var idList = NormalizeIds(ids);
        if (idList.Count == 0)
        {
            return new Dictionary<string, FoodicsCategoryInfoDto>();
        }

        var token = GetAccessToken(accessToken);
        var url = $"categories?filter[id]={Uri.EscapeDataString(string.Join(",", idList))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Foodics categories request failed. StatusCode={StatusCode}, Body={Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<FoodicsListEnvelope<FoodicsCategoryInfoDto>>(_jsonOptions, cancellationToken);

        if (payload?.Data == null || payload.Data.Count == 0)
        {
            // Debug logging for empty result
            _logger.LogWarning("Foodics categories returned 0 items. Requested IDs: {Ids}. Request URL: {Url}", string.Join(",", idList), url);
        }

        var result = new Dictionary<string, FoodicsCategoryInfoDto>(StringComparer.OrdinalIgnoreCase);
        if (payload?.Data != null)
        {
            foreach (var item in payload.Data)
            {
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    result[item.Id] = item;
                }
            }
        }
        return result;
    }

    public async Task<Dictionary<string, FoodicsProductInfoDto>> GetProductsByIdsAsync(
        IEnumerable<string> ids,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        var idList = NormalizeIds(ids);
        if (idList.Count == 0)
        {
            return new Dictionary<string, FoodicsProductInfoDto>();
        }

        var token = GetAccessToken(accessToken);
        var url = $"products?filter[id]={Uri.EscapeDataString(string.Join(",", idList))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Foodics products request failed. StatusCode={StatusCode}, Body={Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<FoodicsListEnvelope<FoodicsProductInfoDto>>(_jsonOptions, cancellationToken);

        if (payload?.Data == null || payload.Data.Count == 0)
        {
            _logger.LogWarning("Foodics products returned 0 items. Requested IDs: {Ids}. Request URL: {Url}", string.Join(",", idList), url);
        }

        var result = new Dictionary<string, FoodicsProductInfoDto>(StringComparer.OrdinalIgnoreCase);
        if (payload?.Data != null)
        {
            foreach (var item in payload.Data)
            {
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    result[item.Id] = item;
                }
            }
        }
        return result;
    }

    public async Task<Dictionary<string, FoodicsProductDetailDto>> GetProductsWithIncludesByIdsAsync(
        IEnumerable<string> ids,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        var idList = NormalizeIds(ids);
        if (idList.Count == 0)
        {
            return new Dictionary<string, FoodicsProductDetailDto>();
        }

        var token = GetAccessToken(accessToken);
        var includes = "category,price_tags,tax_group,tags,branches,ingredients.branches,modifiers,modifiers.options,modifiers.options.branches,discounts,timed_events,groups";
        var url = $"products?filter[id]={Uri.EscapeDataString(string.Join(",", idList))}&include={Uri.EscapeDataString(includes)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogInformation("Requesting Foodics products with full includes for {Count} product IDs", idList.Count);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Foodics products with includes request failed. StatusCode={StatusCode}, Body={Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<FoodicsListEnvelope<FoodicsProductDetailDto>>(_jsonOptions, cancellationToken);

        if (payload?.Data == null || payload.Data.Count == 0)
        {
            _logger.LogWarning("Foodics products (with includes) returned 0 items. Requested IDs: {Ids}. Request URL: {Url}", string.Join(",", idList), url);
        }

        var result = new Dictionary<string, FoodicsProductDetailDto>(StringComparer.OrdinalIgnoreCase);
        if (payload?.Data != null)
        {
            foreach (var item in payload.Data)
            {
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    result[item.Id] = item;
                }
            }
        }

        _logger.LogInformation("Successfully fetched {Count} products with full includes", result.Count);
        return result;
    }

    /// <summary>
    /// Fetches ALL products with full includes (no ID filtering). Handles pagination automatically.
    /// This method is optimized for menu sync operations where all products need to be retrieved.
    /// </summary>
    /// <param name="branchId">Optional branch ID to filter products by branch availability</param>
    /// <param name="perPage">Number of products per page (default: 100, max recommended: 250)</param>
    /// <param name="includeDeleted">Whether to include deleted products (useful for Sandbox testing)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of all products keyed by product ID</returns>
    public async Task<Dictionary<string, FoodicsProductDetailDto>> GetAllProductsWithIncludesAsync(
    string? branchId = null,
    string? accessToken = null,
    int perPage = 100,
    bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var token = GetAccessToken(accessToken);

        // ✅ FIXED: Include all required fields as per your curl command
        var includes = "category,price_tags,tax_group,tags,branches,ingredients.branches,modifiers,modifiers.options,modifiers.options.branches,discounts,timed_events,groups";

        var result = new Dictionary<string, FoodicsProductDetailDto>(StringComparer.OrdinalIgnoreCase);

        int currentPage = 1;
        int totalFetched = 0;
        int? totalProducts = null;
        int? actualPerPage = null;

        _logger.LogInformation(
            "[Foodics GetAllProducts] Starting to fetch ALL Foodics products with full includes. " +
            "Branch={BranchId}, PerPage={PerPage}, IncludeDeleted={IncludeDeleted}",
            branchId ?? "<all>",
            perPage,
            includeDeleted);

        while (true)
        {
            try
            {
                var queryParams = new List<string>
            {
                $"include={Uri.EscapeDataString(includes)}",
                $"page={currentPage}",
                $"per_page={perPage}"
            };

                // Optionally filter by branch if specified
                if (!string.IsNullOrWhiteSpace(branchId))
                {
                    queryParams.Add($"filter[branch_id]={Uri.EscapeDataString(branchId)}");
                }

                var url = $"products?{string.Join("&", queryParams)}";

                _logger.LogInformation(
                    "[Foodics GetAllProducts] Sending request to Foodics API. Page={Page}, URL={URL}",
                    currentPage,
                    url);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                _logger.LogInformation(
                    "[Foodics GetAllProducts] Fetching Foodics products page {Page} (total fetched so far: {TotalFetched})",
                    currentPage,
                    totalFetched);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                _logger.LogInformation(
                    "[Foodics GetAllProducts] Foodics API response received. Page={Page}, StatusCode={StatusCode}, ContentLength={ContentLength}",
                    currentPage,
                    (int)response.StatusCode,
                    response.Content.Headers.ContentLength);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "[Foodics GetAllProducts] Foodics products pagination request failed. Page={Page}, StatusCode={StatusCode}, Body={Body}",
                        currentPage,
                        (int)response.StatusCode,
                        body);
                    response.EnsureSuccessStatusCode();
                }

                _logger.LogInformation("[Foodics GetAllProducts] Starting to deserialize response for page {Page}", currentPage);

                // Try to deserialize as paginated response first (with meta)
                var paginatedPayload = await response.Content.ReadFromJsonAsync<FoodicsProductAvailabilityListEnvelope>(_jsonOptions, cancellationToken);

                _logger.LogInformation(
                    "[Foodics GetAllProducts] Deserialization completed for page {Page}. HasData={HasData}, DataCount={DataCount}",
                    currentPage,
                    paginatedPayload?.Data != null,
                    paginatedPayload?.Data?.Count ?? 0);

                if (paginatedPayload != null && paginatedPayload.Data != null)
                {
                    // ✅ FIXED: Capture pagination metadata from API response
                    if (paginatedPayload.Meta != null)
                    {
                        if (paginatedPayload.Meta.Total.HasValue)
                        {
                            totalProducts = paginatedPayload.Meta.Total.Value;
                        }
                        if (paginatedPayload.Meta.PerPage.HasValue)
                        {
                            actualPerPage = paginatedPayload.Meta.PerPage.Value;
                        }

                        // Calculate last page if we have the data
                        int? calculatedLastPage = null;
                        if (totalProducts.HasValue && actualPerPage.HasValue && actualPerPage.Value > 0)
                        {
                            calculatedLastPage = (int)Math.Ceiling((double)totalProducts.Value / actualPerPage.Value);
                        }

                        _logger.LogInformation(
                            "[Foodics GetAllProducts] Pagination Info: CurrentPage={CurrentPage}, CalculatedLastPage={LastPage}, Total={Total}, PerPage={PerPage}",
                            paginatedPayload.Meta.CurrentPage,
                            calculatedLastPage?.ToString() ?? "<unknown>",
                            totalProducts,
                            actualPerPage);
                    }

                    // Process products
                    int itemsInThisPage = 0;
                    foreach (var item in paginatedPayload.Data)
                    {
                        // Filter out deleted products (Foodics API doesn't filter them properly)
                        // Unless includeDeleted is true (useful for Sandbox testing)
                        bool shouldInclude = !string.IsNullOrWhiteSpace(item.Id) && !result.ContainsKey(item.Id);

                        if (!includeDeleted)
                        {
                            // Check if DeletedAt is null, empty, or whitespace (not deleted)
                            shouldInclude = shouldInclude && (item.DeletedAt == null || string.IsNullOrWhiteSpace(item.DeletedAt));
                        }

                        if (shouldInclude)
                        {
                            result[item.Id] = item;
                            totalFetched++;
                            itemsInThisPage++;

                            _logger.LogDebug(
                                "Including product: Id={ProductId}, Name={ProductName}, DeletedAt={DeletedAt}",
                                item.Id, item.Name, item.DeletedAt ?? "<null>");
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Excluding product: Id={ProductId}, Name={ProductName}, DeletedAt={DeletedAt}, Reason={Reason}",
                                item.Id, item.Name, item.DeletedAt ?? "<null>",
                                string.IsNullOrWhiteSpace(item.Id) ? "EmptyId" :
                                result.ContainsKey(item.Id) ? "Duplicate" :
                                !string.IsNullOrWhiteSpace(item.DeletedAt) ? "Deleted" : "Unknown");
                        }
                    }

                    _logger.LogInformation(
                        "[Foodics GetAllProducts] Processed page {Page}. ItemsInPage={ItemsInPage}, ItemsIncluded={ItemsIncluded}, TotalFetched={TotalFetched}, TotalExpected={TotalExpected}",
                        currentPage,
                        paginatedPayload.Data.Count,
                        itemsInThisPage,
                        totalFetched,
                        totalProducts?.ToString() ?? "<unknown>");

                    // ✅ FIXED: Determine if this is the last page
                    bool isLastPage = false;

                    // Method 1: If we got 0 items, we're done
                    if (paginatedPayload.Data.Count == 0)
                    {
                        isLastPage = true;
                        _logger.LogInformation(
                            "[Foodics GetAllProducts] Reached last page - received 0 items on page {Page}",
                            currentPage);
                    }
                    // Method 2: If we've fetched all expected items based on total
                    else if (totalProducts.HasValue && totalFetched >= totalProducts.Value)
                    {
                        isLastPage = true;
                        _logger.LogInformation(
                            "[Foodics GetAllProducts] Reached last page - fetched all expected items. TotalFetched={TotalFetched}, TotalExpected={TotalExpected}",
                            totalFetched,
                            totalProducts.Value);
                    }
                    // Method 3: Calculate if we're on the last page
                    else if (totalProducts.HasValue && actualPerPage.HasValue && actualPerPage.Value > 0)
                    {
                        int calculatedLastPage = (int)Math.Ceiling((double)totalProducts.Value / actualPerPage.Value);
                        if (currentPage >= calculatedLastPage)
                        {
                            isLastPage = true;
                            _logger.LogInformation(
                                "[Foodics GetAllProducts] Reached calculated last page. CurrentPage={CurrentPage}, CalculatedLastPage={LastPage}",
                                currentPage,
                                calculatedLastPage);
                        }
                    }

                    if (isLastPage)
                    {
                        _logger.LogInformation(
                            "[Foodics GetAllProducts] Pagination complete. TotalFetched={TotalFetched}, ExpectedTotal={ExpectedTotal}, PagesProcessed={Pages}",
                            totalFetched,
                            totalProducts?.ToString() ?? "<unknown>",
                            currentPage);
                        break;
                    }
                }
                else
                {
                    _logger.LogInformation("[Foodics GetAllProducts] Trying fallback deserialization for page {Page}", currentPage);

                    // Fallback to non-paginated response format
                    var payload = await response.Content.ReadFromJsonAsync<FoodicsListEnvelope<FoodicsProductDetailDto>>(_jsonOptions, cancellationToken);
                    if (payload?.Data == null || payload.Data.Count == 0)
                    {
                        _logger.LogInformation("[Foodics GetAllProducts] No more data available. Breaking pagination loop.");
                        break;
                    }

                    foreach (var item in payload.Data)
                    {
                        // Filter out deleted products (Foodics API doesn't filter them properly)
                        // Unless includeDeleted is true (useful for Sandbox testing)
                        bool shouldInclude = !string.IsNullOrWhiteSpace(item.Id) && !result.ContainsKey(item.Id);

                        if (!includeDeleted)
                        {
                            // Check if DeletedAt is null, empty, or whitespace (not deleted)
                            shouldInclude = shouldInclude && (item.DeletedAt == null || string.IsNullOrWhiteSpace(item.DeletedAt));
                        }

                        if (shouldInclude)
                        {
                            result[item.Id] = item;
                            totalFetched++;

                            _logger.LogDebug(
                                "Including product: Id={ProductId}, Name={ProductName}, DeletedAt={DeletedAt}",
                                item.Id, item.Name, item.DeletedAt ?? "<null>");
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Excluding product: Id={ProductId}, Name={ProductName}, DeletedAt={DeletedAt}, Reason={Reason}",
                                item.Id, item.Name, item.DeletedAt ?? "<null>",
                                string.IsNullOrWhiteSpace(item.Id) ? "EmptyId" :
                                result.ContainsKey(item.Id) ? "Duplicate" :
                                !string.IsNullOrWhiteSpace(item.DeletedAt) ? "Deleted" : "Unknown");
                        }
                    }

                    // If we got 0 items, we're done
                    if (payload.Data.Count == 0)
                    {
                        _logger.LogInformation(
                            "[Foodics GetAllProducts] Fallback pagination reached last page - 0 items. TotalFetched={TotalFetched}",
                            totalFetched);
                        break;
                    }
                }

                currentPage++;

                // ✅ ADDED: Safety check to prevent infinite loops
                if (currentPage > 1000)
                {
                    _logger.LogWarning(
                        "[Foodics GetAllProducts] Reached safety limit of 1000 pages. Breaking pagination. TotalFetched={TotalFetched}",
                        totalFetched);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Foodics GetAllProducts] Error fetching products from Foodics API. Page={Page}, TotalFetched={TotalFetched}. " +
                    "Error: {ErrorMessage}",
                    currentPage, totalFetched, ex.Message);

                // If it's the first page, re-throw the exception
                if (currentPage == 1)
                {
                    throw;
                }

                // If we have some products already, log warning and break
                _logger.LogWarning(
                    "[Foodics GetAllProducts] Stopping pagination due to error on page {Page}. " +
                    "Successfully fetched {TotalFetched} products from previous pages.",
                    currentPage, totalFetched);
                break;
            }
        }

        if (result.Count == 0)
        {
            _logger.LogWarning(
                "⚠️ [Foodics Sync] GetAllProductsWithIncludesAsync returned 0 products after checking ALL pages. " +
                "TotalFetched={TotalFetched}, PagesScanned={Pages}, Branch={BranchId}. " +
                "This usually means either: " +
                "1. The account has no products used in this branch. " +
                "2. All products are deleted/inactive. " +
                "3. JSON Deserialization mismatch (IDs not matching 'id' property).",
                totalFetched, currentPage - 1, branchId ?? "<all>");
        }
        else
        {
            _logger.LogInformation(
                "✅ Successfully fetched ALL {Count} products with full includes (expected: {Expected}, branch: {BranchId}, pages: {Pages})",
                result.Count,
                totalProducts?.ToString() ?? "<unknown>",
                branchId ?? "<all>",
                currentPage - 1);
        }

        return result;
    }
    public async Task<Dictionary<string, FoodicsGroupInfoDto>> GetGroupsByIdsAsync(
        IEnumerable<string> ids,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
{
    var idList = NormalizeIds(ids);
    if (idList.Count == 0)
    {
        return new Dictionary<string, FoodicsGroupInfoDto>();
    }

    var token = GetAccessToken(accessToken);
    var url = $"groups?filter[id]={Uri.EscapeDataString(string.Join(",", idList))}";
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    _logger.LogInformation("Requesting Foodics groups for IDs: {GroupIds}", string.Join(", ", idList));

    var response = await _httpClient.SendAsync(request, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Foodics groups request failed. StatusCode={StatusCode}, Body={Body}", (int)response.StatusCode, body);
        response.EnsureSuccessStatusCode();
    }

    var payload = await response.Content.ReadFromJsonAsync<FoodicsListEnvelope<FoodicsGroupInfoDto>>(_jsonOptions, cancellationToken);

    if (payload?.Data == null || payload.Data.Count == 0)
    {
        _logger.LogWarning("Foodics groups returned 0 items. Requested IDs: {Ids}. Request URL: {Url}", string.Join(",", idList), url);
    }

    var result = new Dictionary<string, FoodicsGroupInfoDto>(StringComparer.OrdinalIgnoreCase);
    if (payload?.Data != null)
    {
        foreach (var item in payload.Data)
        {
            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                result[item.Id] = item;
            }
        }
    }
    return result;
}

public async Task<FoodicsProductAvailabilityListEnvelope> GetProductsWithAvailabilityAsync(
    string accessToken,
    int page = 1,
    int perPage = 100,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(accessToken))
    {
        throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken));
    }

    var url = $"products?include=price_tags,branches&page={page}&per_page={perPage}";
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    _logger.LogInformation("Requesting Foodics products with availability (page {Page}, per_page {PerPage})", page, perPage);

    var response = await _httpClient.SendAsync(request, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Foodics products availability request failed. StatusCode={StatusCode}, Body={Body}", (int)response.StatusCode, body);
        response.EnsureSuccessStatusCode();
    }

    var payload = await response.Content.ReadFromJsonAsync<FoodicsProductAvailabilityListEnvelope>(_jsonOptions, cancellationToken);
    if (payload is null)
    {
        throw new InvalidOperationException("Unable to deserialize Foodics products availability response.");
    }

    _logger.LogInformation(
        "Fetched {Count} products with availability (page {Page}, total: {Total})",
        payload.Data?.Count ?? 0,
        page,
        payload.Meta?.Total ?? 0);

    return payload;
}

private static List<string> NormalizeIds(IEnumerable<string> ids)
{
    return ids
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => id.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

private static string EnsureEndsWithSlash(string url)
{
    return url.EndsWith("/") ? url : url + "/";
}
}


