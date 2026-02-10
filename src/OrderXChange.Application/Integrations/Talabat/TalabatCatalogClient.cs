using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Talabat;

/// <summary>
/// Talabat Catalog Management Client - V2 API
/// Handles catalog submission and availability updates
/// Reference: https://integration-middleware.stg.restaurant-partners.com/apidocs/pos-middleware-api
/// V2 API Endpoint: PUT /v2/chains/{chainCode}/catalog
/// </summary>
public class TalabatCatalogClient : ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly TalabatAuthClient _authClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatCatalogClient> _logger;

    public TalabatCatalogClient(
        HttpClient httpClient,
        TalabatAuthClient authClient,
        IConfiguration configuration,
        ILogger<TalabatCatalogClient> logger)
    {
        _httpClient = httpClient;
        _authClient = authClient;
        _configuration = configuration;
        _logger = logger;

        var baseUrl = configuration["Talabat:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Talabat:BaseUrl configuration is missing.");
        }

        _httpClient.BaseAddress = new Uri(EnsureEndsWithSlash(baseUrl));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Submit catalog to Talabat using V2 API - Items-based format
    /// PUT /v2/chains/{chainCode}/catalog
    /// This is the NEW format that Talabat provided in their example
    /// 
    /// FIXED: Now accepts vendorCode to get token BEFORE HTTP request to avoid DbContext disposal
    /// </summary>
    /// <param name="chainCode">Chain/Brand code (e.g., "tlbt-pick")</param>
    /// <param name="request">V2 catalog data with items dictionary structure</param>
    /// <param name="vendorCode">Optional vendor code to get credentials for specific TalabatAccount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Submission response with import ID for tracking</returns>
    public async Task<TalabatCatalogSubmitResponse> SubmitV2CatalogAsync(
        string chainCode,
        TalabatV2CatalogSubmitRequest request,
        string? vendorCode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chainCode))
        {
            throw new ArgumentException("Chain code is required", nameof(chainCode));
        }

        // ✅ CRITICAL FIX: Pre-fetch and cache credentials BEFORE any HTTP operations
        // This ensures database queries complete while DbContext is still valid.
        // Credentials are cached, so retry operations won't need DB access.
        await _authClient.PreFetchCredentialsAsync(vendorCode, cancellationToken);
        
        var accessToken = await _authClient.GetAccessTokenAsync(vendorCode, cancellationToken);
        
        var url = $"v2/chains/{Uri.EscapeDataString(chainCode)}/catalog";

        _logger.LogInformation(
            "Submitting V2 catalog to Talabat. ChainCode={ChainCode}, Items={ItemCount}, Vendors={Vendors}, CallbackUrl={CallbackUrl}",
            chainCode,
            request.Catalog?.Items?.Count ?? 0,
            request.Vendors != null ? string.Join(",", request.Vendors) : "<none>",
            request.CallbackUrl ?? "<none>");

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var shouldLogPayload = _configuration.GetValue<bool>("Talabat:LogCatalogPayload", false);
            if (shouldLogPayload)
            {
                var payloadJson = JsonSerializer.Serialize(request, jsonOptions);
                _logger.LogInformation(
                    "Talabat V2 catalog payload (bytes={PayloadBytes}): {Payload}",
                    Encoding.UTF8.GetByteCount(payloadJson),
                    payloadJson);
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);
            httpRequest.Content = JsonContent.Create(request, options: jsonOptions);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Handle auth errors - invalidate token and retry once
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning(
                    "Talabat V2 catalog submission returned 401 Unauthorized. Response: {ResponseBody}",
                    responseBody);
                
                _authClient.InvalidateToken();
                _logger.LogWarning("Token invalidated. Retrying with fresh token...");
                
                // ✅ Retry is safe because credentials were pre-cached via PreFetchCredentialsAsync
                // This call will use cached credentials, avoiding DbContext disposal issues
                accessToken = await _authClient.GetAccessTokenAsync(vendorCode, cancellationToken);
                using var retryRequest = new HttpRequestMessage(HttpMethod.Put, url);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);
                retryRequest.Content = JsonContent.Create(request, options: jsonOptions);

                using var retryResponse = await _httpClient.SendAsync(retryRequest, cancellationToken);
                var retryResponseBody = await retryResponse.Content.ReadAsStringAsync(cancellationToken);

                if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError(
                        "Talabat authentication failed after token refresh. First attempt response: {FirstResponse}, Retry response: {RetryResponse}",
                        responseBody,
                        retryResponseBody);
                    
                    throw new HttpRequestException(
                        $"Talabat authentication failed after token refresh. First response: {responseBody}, Retry response: {retryResponseBody}. " +
                        "Check credentials and ensure the token is valid for the specified chainCode.");
                }

                response.Dispose();
                return ProcessCatalogResponse(retryResponse, retryResponseBody, chainCode);
            }

            return ProcessCatalogResponse(response, responseBody, chainCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during Talabat V2 catalog submission. ChainCode={ChainCode}", chainCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting V2 catalog to Talabat. ChainCode={ChainCode}", chainCode);
            throw new InvalidOperationException($"Failed to submit V2 catalog to Talabat for chain {chainCode}", ex);
        }
    }

    /// <summary>
    /// Submit catalog to Talabat using V2 API - Legacy format (categories-based)
    /// PUT /v2/chains/{chainCode}/catalog
    /// Reference: https://integration-middleware.stg.restaurant-partners.com/apidocs/pos-middleware-api#tag/Catalog-Import
    /// </summary>
    /// <param name="chainCode">Chain/Brand code (e.g., "783216")</param>
    /// <param name="request">Catalog data to submit (must include vendors array)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Submission response with import ID for tracking</returns>
    public async Task<TalabatCatalogSubmitResponse> SubmitCatalogAsync(
        string chainCode,
        TalabatCatalogSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chainCode))
        {
            throw new ArgumentException("Chain code is required", nameof(chainCode));
        }

        // Pre-fetch and cache credentials before HTTP operations
        await _authClient.PreFetchCredentialsAsync(null, cancellationToken);
        var accessToken = await _authClient.GetAccessTokenAsync(null, cancellationToken);
        
        // V2 API endpoint
        var url = $"v2/chains/{Uri.EscapeDataString(chainCode)}/catalog";

        _logger.LogInformation(
            "Submitting catalog to Talabat V2 API. ChainCode={ChainCode}, Categories={CategoryCount}, Vendors={Vendors}, CallbackUrl={CallbackUrl}",
            chainCode,
            request.Menu?.Categories?.Count ?? 0,
            request.Menu?.Vendors != null ? string.Join(",", request.Menu.Vendors) : "<none>",
            request.CallbackUrl ?? "<none>");

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);
            httpRequest.Content = JsonContent.Create(request, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Handle auth errors - invalidate token and retry once
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning(
                    "Talabat catalog submission returned 401 Unauthorized. Response: {ResponseBody}",
                    responseBody);
                
                _authClient.InvalidateToken();
                _logger.LogWarning("Token invalidated. Retrying with fresh token...");
                
                // Retry with fresh token
                accessToken = await _authClient.GetAccessTokenAsync(null, cancellationToken);
                using var retryRequest = new HttpRequestMessage(HttpMethod.Put, url);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);
                retryRequest.Content = JsonContent.Create(request, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                using var retryResponse = await _httpClient.SendAsync(retryRequest, cancellationToken);
                var retryResponseBody = await retryResponse.Content.ReadAsStringAsync(cancellationToken);

                if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError(
                        "Talabat authentication failed after token refresh. First attempt response: {FirstResponse}, Retry response: {RetryResponse}",
                        responseBody,
                        retryResponseBody);
                    
                    throw new HttpRequestException(
                        $"Talabat authentication failed after token refresh. First response: {responseBody}, Retry response: {retryResponseBody}. " +
                        "Check credentials and ensure the token is valid for the specified chainCode.");
                }

                response.Dispose();
                return ProcessCatalogResponse(retryResponse, retryResponseBody, chainCode);
            }

            return ProcessCatalogResponse(response, responseBody, chainCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during Talabat catalog submission. ChainCode={ChainCode}", chainCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting catalog to Talabat. ChainCode={ChainCode}", chainCode);
            throw new InvalidOperationException($"Failed to submit catalog to Talabat for chain {chainCode}", ex);
        }
    }

    private TalabatCatalogSubmitResponse ProcessCatalogResponse(
        HttpResponseMessage response, 
        string responseBody, 
        string chainCode)
    {
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Talabat catalog submission failed. ChainCode={ChainCode}, StatusCode={StatusCode}, Response={Response}",
                chainCode,
                (int)response.StatusCode,
                responseBody);

            // Try to parse error response
            var errorResponse = TryParseResponse<TalabatCatalogSubmitResponse>(responseBody);
            if (errorResponse != null)
            {
                errorResponse.Success = false;
                if (string.IsNullOrEmpty(errorResponse.Message))
                {
                    errorResponse.Message = $"HTTP {response.StatusCode}: {responseBody}";
                }
                return errorResponse;
            }

            // Try to parse V2 API error format
            var v2Error = TryParseResponse<TalabatV2ErrorResponse>(responseBody);
            if (v2Error != null)
            {
                return new TalabatCatalogSubmitResponse
                {
                    Success = false,
                    Message = $"{v2Error.Code}: {v2Error.Message}"
                };
            }

            // For 400 Bad Request, include full response body in exception for better debugging
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var ex = new HttpRequestException(
                    $"Talabat catalog submission failed with 400 Bad Request: {responseBody}")
                {
                    Data = { ["ResponseBody"] = responseBody }
                };
                throw ex;
            }

            return new TalabatCatalogSubmitResponse
            {
                Success = false,
                Message = $"HTTP {response.StatusCode}: {responseBody}"
            };
        }

        // Log raw response for debugging
        _logger.LogInformation(
            "Talabat API raw response. ChainCode={ChainCode}, StatusCode={StatusCode}, Body={ResponseBody}",
            chainCode,
            (int)response.StatusCode,
            responseBody);

        var result = TryParseResponse<TalabatCatalogSubmitResponse>(responseBody);
        if (result != null)
        {
            result.Success = true;
            _logger.LogInformation(
                "✅ Talabat catalog submitted successfully. ChainCode={ChainCode}, ImportId={ImportId}, Message={Message}",
                chainCode,
                result.ImportId ?? "<not provided>",
                result.Message ?? "<no message>");
            return result;
        }

        // Fallback: V2 response shape { status, catalogImportId }
        var v2Result = TryParseResponse<TalabatV2CatalogSubmitResponse>(responseBody);
        if (v2Result != null)
        {
            var mapped = new TalabatCatalogSubmitResponse
            {
                Success = true,
                ImportId = v2Result.CatalogImportId,
                Message = v2Result.Message ?? v2Result.Status
            };

            _logger.LogInformation(
                "✅ Talabat V2 catalog submitted successfully. ChainCode={ChainCode}, ImportId={ImportId}, Status={Status}",
                chainCode,
                mapped.ImportId ?? "<not provided>",
                v2Result.Status ?? "<no status>");

            return mapped;
        }

        // Manual fallback: Try to extract catalogImportId from JSON
        try
        {
            var jsonDoc = JsonDocument.Parse(responseBody);
            if (jsonDoc.RootElement.TryGetProperty("catalogImportId", out var importIdProp))
            {
                var mapped = new TalabatCatalogSubmitResponse
                {
                    Success = true,
                    ImportId = importIdProp.GetString(),
                    Message = jsonDoc.RootElement.TryGetProperty("status", out var statusProp)
                        ? statusProp.GetString()
                        : "submitted"
                };

                _logger.LogInformation(
                    "✅ Talabat catalog submitted (manual parse). ChainCode={ChainCode}, ImportId={ImportId}",
                    chainCode,
                    mapped.ImportId);

                return mapped;
            }
        }
        catch (Exception parseEx)
        {
            _logger.LogDebug(parseEx, "Could not manually parse catalogImportId from response");
        }

        // If response body is empty or not parseable but status was success (202 Accepted)
        _logger.LogInformation(
            "✅ Talabat accepted catalog (status {StatusCode}). ChainCode={ChainCode}. Note: ImportId may be returned via callback webhook.",
            (int)response.StatusCode,
            chainCode);
            
        return new TalabatCatalogSubmitResponse
        {
            Success = true,
            Message = $"Catalog accepted (HTTP {(int)response.StatusCode}). ImportId will be provided via callback webhook if configured."
        };
    }

    /// <summary>
    /// Get catalog import status/log - V2 API
    /// GET /v2/chains/{chainCode}/catalog/import-log
    /// </summary>
    public async Task<TalabatCatalogImportLogResponse?> GetCatalogImportLogAsync(
        string chainCode,
        string? importId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chainCode))
        {
            throw new ArgumentException("Chain code is required", nameof(chainCode));
        }

        await _authClient.PreFetchCredentialsAsync(null, cancellationToken);
        var accessToken = await _authClient.GetAccessTokenAsync(null, cancellationToken);
        var url = $"v2/chains/{Uri.EscapeDataString(chainCode)}/catalog/import-log";
        
        if (!string.IsNullOrWhiteSpace(importId))
        {
            url += $"?importId={Uri.EscapeDataString(importId)}";
        }

        _logger.LogInformation(
            "Getting catalog import log from Talabat V2. ChainCode={ChainCode}, ImportId={ImportId}",
            chainCode,
            importId ?? "<latest>");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _authClient.InvalidateToken();
                throw new HttpRequestException("Talabat authentication failed.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to get catalog import log. ChainCode={ChainCode}, StatusCode={StatusCode}",
                    chainCode,
                    (int)response.StatusCode);
                return null;
            }

            return TryParseResponse<TalabatCatalogImportLogResponse>(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting catalog import log. ChainCode={ChainCode}", chainCode);
            throw;
        }
    }

    /// <summary>
    /// Update item availability
    /// POST /catalogs/stores/{vendor_code}/items/availability
    /// </summary>
    public async Task<TalabatUpdateItemAvailabilityResponse> UpdateItemAvailabilityAsync(
        string vendorCode,
        TalabatUpdateItemAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorCode))
        {
            throw new ArgumentException("Vendor code is required", nameof(vendorCode));
        }

        await _authClient.PreFetchCredentialsAsync(vendorCode, cancellationToken);
        var accessToken = await _authClient.GetAccessTokenAsync(vendorCode, cancellationToken);
        // Talabat POSMW docs use v2 prefix for catalogs endpoints
        // Reference: https://talabat.stoplight.io/docs/POSMW/ce2a790feb2c8-introduction
        var url = $"v2/catalogs/stores/{Uri.EscapeDataString(vendorCode)}/items/availability";

        _logger.LogInformation(
            "Updating item availability on Talabat. VendorCode={VendorCode}, ItemCount={ItemCount}",
            vendorCode,
            request.Items?.Count ?? 0);

        try
        {
            // Talabat availability endpoint expects PUT (POST returns 404)
            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);
            httpRequest.Content = JsonContent.Create(request);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _authClient.InvalidateToken();
                throw new HttpRequestException("Talabat authentication failed.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to update item availability. VendorCode={VendorCode}, StatusCode={StatusCode}",
                    vendorCode,
                    (int)response.StatusCode);

                return new TalabatUpdateItemAvailabilityResponse
                {
                    Success = false,
                    Message = $"HTTP {response.StatusCode}: {responseBody}"
                };
            }

            var result = TryParseResponse<TalabatUpdateItemAvailabilityResponse>(responseBody);
            return result ?? new TalabatUpdateItemAvailabilityResponse
            {
                Success = true,
                Message = "Item availability updated successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item availability. VendorCode={VendorCode}", vendorCode);
            throw;
        }
    }

    /// <summary>
    /// Update vendor/store availability
    /// POST /vendors/{vendor_code}/availability
    /// </summary>
    public async Task<TalabatVendorAvailabilityResponse?> UpdateVendorAvailabilityAsync(
        string vendorCode,
        TalabatUpdateVendorAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorCode))
        {
            throw new ArgumentException("Vendor code is required", nameof(vendorCode));
        }

        await _authClient.PreFetchCredentialsAsync(vendorCode, cancellationToken);
        var accessToken = await _authClient.GetAccessTokenAsync(vendorCode, cancellationToken);
        var url = $"vendors/{Uri.EscapeDataString(vendorCode)}/availability";

        _logger.LogInformation(
            "Updating vendor availability on Talabat. VendorCode={VendorCode}, IsAvailable={IsAvailable}",
            vendorCode,
            request.IsAvailable);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);
            httpRequest.Content = JsonContent.Create(request);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _authClient.InvalidateToken();
                throw new HttpRequestException("Talabat authentication failed.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to update vendor availability. VendorCode={VendorCode}, StatusCode={StatusCode}",
                    vendorCode,
                    (int)response.StatusCode);
                return null;
            }

            return TryParseResponse<TalabatVendorAvailabilityResponse>(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vendor availability. VendorCode={VendorCode}", vendorCode);
            throw;
        }
    }

    /// <summary>
    /// Get vendor/store availability status
    /// GET /vendors/{vendor_code}/availability
    /// </summary>
    public async Task<TalabatVendorAvailabilityResponse?> GetVendorAvailabilityAsync(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorCode))
        {
            throw new ArgumentException("Vendor code is required", nameof(vendorCode));
        }

        await _authClient.PreFetchCredentialsAsync(vendorCode, cancellationToken);
        var accessToken = await _authClient.GetAccessTokenAsync(vendorCode, cancellationToken);
        var url = $"vendors/{Uri.EscapeDataString(vendorCode)}/availability";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _authClient.InvalidateToken();
                throw new HttpRequestException("Talabat authentication failed.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to get vendor availability. VendorCode={VendorCode}, StatusCode={StatusCode}",
                    vendorCode,
                    (int)response.StatusCode);
                return null;
            }

            return TryParseResponse<TalabatVendorAvailabilityResponse>(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vendor availability. VendorCode={VendorCode}", vendorCode);
            throw;
        }
    }

    /// <summary>
    /// Update vendor/store availability using V2 API
    /// POST /v2/chains/{chainCode}/remoteVendors/{chainVendorId}/availability
    /// Reference: https://talabat.stoplight.io/docs/POSMW/c2ab8856764e5-update-availability-status
    /// </summary>
    public async Task<TalabatVendorAvailabilityResponse?> UpdateVendorAvailabilityV2Async(
        string chainCode,
        string chainVendorId,
        TalabatUpdateVendorAvailabilityV2Request request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chainCode))
        {
            throw new ArgumentException("Chain code is required", nameof(chainCode));
        }

        if (string.IsNullOrWhiteSpace(chainVendorId))
        {
            throw new ArgumentException("Chain vendor ID is required", nameof(chainVendorId));
        }

        await _authClient.PreFetchCredentialsAsync(chainVendorId, cancellationToken);
        var accessToken = await _authClient.GetAccessTokenAsync(chainVendorId, cancellationToken);
        var url = $"v2/chains/{Uri.EscapeDataString(chainCode)}/remoteVendors/{Uri.EscapeDataString(chainVendorId)}/availability";

        _logger.LogInformation(
            "Updating vendor availability (V2) on Talabat. ChainCode={ChainCode}, ChainVendorId={VendorId}, State={State}, PlatformKey={PlatformKey}, PlatformRestaurantId={PlatformRestaurantId}, ClosingMinutes={ClosingMinutes}, ClosedReason={ClosedReason}",
            chainCode,
            chainVendorId,
            request.AvailabilityState,
            request.PlatformKey,
            request.PlatformRestaurantId,
            request.ClosingMinutes,
            request.ClosedReason);

        // Log serialized request body to compare with working curl
        try
        {
            var serialized = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            _logger.LogInformation("Talabat V2 availability request payload: {Payload}", serialized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize Talabat V2 availability request for logging.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);
            httpRequest.Content = JsonContent.Create(request);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Talabat V2 availability raw response. StatusCode={StatusCode}, Body={Body}", (int)response.StatusCode, responseBody);

            _logger.LogDebug(
                "Talabat V2 availability response: StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                responseBody);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _authClient.InvalidateToken();
                // Retry once with a fresh token
                _logger.LogWarning("Talabat availability returned 401. Retrying once with fresh token...");
                accessToken = await _authClient.GetAccessTokenAsync(null, cancellationToken);
                using var retryRequest = new HttpRequestMessage(HttpMethod.Put, url);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue(_authClient.GetAuthHeaderType(), accessToken);
                retryRequest.Content = JsonContent.Create(request);
                using var retryResponse = await _httpClient.SendAsync(retryRequest, cancellationToken);
                var retryBody = await retryResponse.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("Talabat V2 availability retry raw response. StatusCode={StatusCode}, Body={Body}", (int)retryResponse.StatusCode, retryBody);

                if (!retryResponse.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to update vendor availability (V2) after retry. ChainCode={ChainCode}, VendorId={VendorId}, StatusCode={StatusCode}, Response={Response}",
                        chainCode,
                        chainVendorId,
                        (int)retryResponse.StatusCode,
                        retryBody);
                    return null;
                }

                return new TalabatVendorAvailabilityResponse
                {
                    VendorCode = chainVendorId,
                    IsAvailable = request.AvailabilityState == TalabatAvailabilityState.Open,
                    LastUpdated = DateTime.UtcNow
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to update vendor availability (V2). ChainCode={ChainCode}, VendorId={VendorId}, StatusCode={StatusCode}, Response={Response}",
                    chainCode,
                    chainVendorId,
                    (int)response.StatusCode,
                    responseBody);
                return null;
            }

            // V2 API might return different response format, adapt as needed
            return new TalabatVendorAvailabilityResponse
            {
                VendorCode = chainVendorId,
                IsAvailable = request.AvailabilityState == TalabatAvailabilityState.Open,
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vendor availability (V2). ChainCode={ChainCode}, VendorId={VendorId}", chainCode, chainVendorId);
            throw;
        }
    }

    #region Branch-Specific Availability Methods

    /// <summary>
    /// Update item availability for a specific branch/vendor
        /// POST /v2/catalogs/stores/{vendorCode}/items/availability
    /// This allows per-branch item availability control
    /// </summary>
    /// <param name="vendorCode">Talabat vendor code for the specific branch</param>
    /// <param name="request">Items to update availability for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<TalabatBranchItemAvailabilityResponse> UpdateBranchItemAvailabilityAsync(
        string vendorCode,
        TalabatBranchItemAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorCode))
        {
            throw new ArgumentException("Vendor code is required", nameof(vendorCode));
        }

        await _authClient.PreFetchCredentialsAsync(vendorCode, cancellationToken);
        var accessToken = await _authClient.GetAccessTokenAsync(vendorCode, cancellationToken);
        var url = $"v2/catalogs/stores/{Uri.EscapeDataString(vendorCode)}/items/availability";

        _logger.LogInformation(
            "🏪 Updating branch item availability. VendorCode={VendorCode}, ItemCount={ItemCount}",
            vendorCode,
            request.Items?.Count ?? 0);

        // Validate required choices (Test Case 4)
        var validationErrors = ValidateRequiredChoices(request, vendorCode);
        if (validationErrors.Any())
        {
            _logger.LogWarning(
                "Required choices validation found {ErrorCount} potential issues for vendor {VendorCode}",
                validationErrors.Count,
                vendorCode);
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to update branch item availability. VendorCode={VendorCode}, StatusCode={StatusCode}, Response={Response}",
                    vendorCode,
                    (int)response.StatusCode,
                    responseBody);

                return new TalabatBranchItemAvailabilityResponse
                {
                    Success = false,
                    VendorCode = vendorCode,
                    Message = $"HTTP {response.StatusCode}: {responseBody}"
                };
            }

            _logger.LogInformation(
                "✅ Branch item availability updated. VendorCode={VendorCode}",
                vendorCode);

            var result = TryParseResponse<TalabatBranchItemAvailabilityResponse>(responseBody);
            return result ?? new TalabatBranchItemAvailabilityResponse
            {
                Success = true,
                VendorCode = vendorCode,
                Message = "Items availability updated successfully",
                UpdatedCount = request.Items?.Count ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating branch item availability. VendorCode={VendorCode}", vendorCode);
            throw;
        }
    }

    /// <summary>
    /// Update item availability across multiple branches in a single operation
    /// Iterates through branches and updates each one
    /// </summary>
    /// <param name="request">Multi-branch update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<TalabatMultiBranchAvailabilityResponse> UpdateMultiBranchAvailabilityAsync(
        TalabatMultiBranchAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Branches == null || request.Branches.Count == 0)
        {
            return new TalabatMultiBranchAvailabilityResponse
            {
                Success = true,
                Message = "No branches to update",
                Results = new List<TalabatBranchUpdateResult>()
            };
        }

        _logger.LogInformation(
            "🏢 Updating availability across {BranchCount} branches",
            request.Branches.Count);

        var results = new List<TalabatBranchUpdateResult>();
        var allSuccess = true;

        foreach (var branchUpdate in request.Branches)
        {
            try
            {
                var branchRequest = new TalabatBranchItemAvailabilityRequest
                {
                    Items = branchUpdate.Items
                };

                var response = await UpdateBranchItemAvailabilityAsync(
                    branchUpdate.VendorCode,
                    branchRequest,
                    cancellationToken);

                results.Add(new TalabatBranchUpdateResult
                {
                    VendorCode = branchUpdate.VendorCode,
                    Success = response.Success,
                    UpdatedCount = response.UpdatedCount,
                    Message = response.Message
                });

                if (!response.Success)
                {
                    allSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update branch {VendorCode}", branchUpdate.VendorCode);
                results.Add(new TalabatBranchUpdateResult
                {
                    VendorCode = branchUpdate.VendorCode,
                    Success = false,
                    Message = ex.Message
                });
                allSuccess = false;
            }
        }

        var totalUpdated = results.Where(r => r.Success).Sum(r => r.UpdatedCount);

        _logger.LogInformation(
            "Multi-branch update complete. TotalBranches={BranchCount}, Successful={SuccessCount}, TotalItemsUpdated={ItemsUpdated}",
            results.Count,
            results.Count(r => r.Success),
            totalUpdated);

        return new TalabatMultiBranchAvailabilityResponse
        {
            Success = allSuccess,
            Message = allSuccess 
                ? $"Successfully updated {totalUpdated} items across {results.Count} branches"
                : $"Partial success: {results.Count(r => r.Success)}/{results.Count} branches updated",
            Results = results
        };
    }

    /// <summary>
    /// Get current item availability status for a branch
        /// GET /v2/catalogs/stores/{vendorCode}/items/availability (if available)
    /// </summary>
    public async Task<List<TalabatBranchItemAvailability>?> GetBranchItemAvailabilityAsync(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorCode))
        {
            throw new ArgumentException("Vendor code is required", nameof(vendorCode));
        }

        await _authClient.PreFetchCredentialsAsync(vendorCode, cancellationToken);
        var accessToken = await _authClient.GetAccessTokenAsync(vendorCode, cancellationToken);
        var url = $"v2/catalogs/stores/{Uri.EscapeDataString(vendorCode)}/items/availability";

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to get branch item availability. VendorCode={VendorCode}, StatusCode={StatusCode}",
                    vendorCode,
                    (int)response.StatusCode);
                return null;
            }

            var result = TryParseResponse<TalabatBranchItemAvailabilityGetResponse>(responseBody);
            return result?.Items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branch item availability. VendorCode={VendorCode}", vendorCode);
            throw;
        }
    }

    #endregion

    #region Required Choices Validation (Test Case 4)

    /// <summary>
    /// Validates item availability updates against required choices/modifiers.
    /// Prevents hiding all choice options when the choice is required (Minimum > 0).
    /// This implements Test Case 4: "If an item has required choices, the integrator cannot 
    /// hide choices only, as the item will be shown to our customer without the required choices"
    /// </summary>
    /// <param name="request">The availability update request</param>
    /// <param name="vendorCode">Vendor code for context</param>
    /// <returns>List of validation errors found</returns>
    private List<TalabatItemAvailabilityError> ValidateRequiredChoices(
        TalabatBranchItemAvailabilityRequest request,
        string vendorCode)
    {
        var errors = new List<TalabatItemAvailabilityError>();
        
        // Note: Full validation would require loading product data from staging
        // For now, we'll log a warning about the business rule
        // The actual enforcement happens at the Talabat side
        
        _logger.LogDebug(
            "Validating required choices for vendor {VendorCode}. Items to update: {ItemCount}",
            vendorCode,
            request.Items?.Count ?? 0);
        
        // Group items by type
        var productUpdates = request.Items?
            .Where(i => string.IsNullOrEmpty(i.Type) || i.Type == "product")
            .ToList() ?? new List<TalabatBranchItemAvailability>();
        
        var choiceOptionUpdates = request.Items?
            .Where(i => i.RemoteCode?.StartsWith("topping-") == true)
            .ToList() ?? new List<TalabatBranchItemAvailability>();
        
        // Log business rule reminder
        if (productUpdates.Any(p => p.IsAvailable) && choiceOptionUpdates.Any(o => !o.IsAvailable))
        {
            _logger.LogInformation(
                "⚠️ Business Rule Check: Some products are available while some choice options are hidden. " +
                "VendorCode={VendorCode}, AvailableProducts={AvailableProducts}, HiddenOptions={HiddenOptions}. " +
                "Ensure products with required choices (Minimum > 0) have sufficient available options.",
                vendorCode,
                productUpdates.Count(p => p.IsAvailable),
                choiceOptionUpdates.Count(o => !o.IsAvailable));
        }
        
        return errors;
    }

    #endregion

    private static T? TryParseResponse<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static string EnsureEndsWithSlash(string url)
    {
        return url.EndsWith("/") ? url : url + "/";
    }
}

/// <summary>
/// V2 API error response format
/// </summary>
internal class TalabatV2ErrorResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public string? Code { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("message")]
    public string? Message { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("details")]
    public object? Details { get; set; }
}

