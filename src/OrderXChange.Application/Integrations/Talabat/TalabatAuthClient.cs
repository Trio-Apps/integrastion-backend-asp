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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Talabat;

/// <summary>
/// Talabat Authentication Client
/// Handles login and token management for Talabat Integration Middleware V2 API
/// Reference: https://integration-middleware.stg.restaurant-partners.com/apidocs/pos-middleware-api
/// 
/// UPDATED: Now uses TalabatAccountService for multi-tenant credential management
/// </summary>
public class TalabatAuthClient : ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly TalabatAccountService _accountService;
    private readonly ILogger<TalabatAuthClient> _logger;

    private const string TokenCacheKey = "Talabat:JwtAccessToken";
    private const string TokenExpiryCacheKey = "Talabat:JwtTokenExpiry";
    private const string CredentialsCacheKeyPrefix = "Talabat:Credentials:";
    private const int TokenExpiryBufferMinutes = 5;
    private const int CredentialsCacheMinutes = 30;

    public TalabatAuthClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache,
        TalabatAccountService accountService,
        ILogger<TalabatAuthClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
        _accountService = accountService;
        _logger = logger;

        var baseUrl = configuration["Talabat:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Talabat:BaseUrl configuration is missing.");
        }

        _httpClient.BaseAddress = new Uri(EnsureEndsWithSlash(baseUrl));
        // Note: We don't set default Accept header here because login endpoint uses form-urlencoded
        // Other endpoints will set Accept: application/json explicitly
    }

    /// <summary>
    /// Gets a valid JWT Bearer token for Talabat V2 API requests.
    /// Automatically refreshes token if expired or about to expire.
    /// If StaticToken is configured, it will be used instead of login.
    /// 
    /// UPDATED: Now supports vendorCode parameter for multi-tenant authentication
    /// </summary>
    /// <param name="vendorCode">Optional vendor code to get credentials for specific TalabatAccount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<string> GetAccessTokenAsync(string? vendorCode = null, CancellationToken cancellationToken = default)
    {
        // Optional: use static token only when explicitly enabled
        var useStaticToken = _configuration.GetValue<bool>("Talabat:UseStaticToken", false);
        var staticToken = _configuration["Talabat:StaticToken"];
        if (useStaticToken && !string.IsNullOrWhiteSpace(staticToken))
        {
            _logger.LogDebug("Using Talabat static token from configuration (UseStaticToken=true).");
            return staticToken;
        }

        // Optional: disable token cache to force fresh login (helpful when tokens expire quickly)
        var useTokenCache = _configuration.GetValue<bool>("Talabat:UseTokenCache", true);
        if (!useTokenCache)
        {
            _logger.LogInformation("Talabat token cache disabled via configuration. Fetching fresh token.");
            var loginResponseNoCache = await LoginAsync(vendorCode, cancellationToken);
            if (string.IsNullOrEmpty(loginResponseNoCache.AccessToken))
            {
                throw new InvalidOperationException("Failed to obtain Talabat access token");
            }
            return loginResponseNoCache.AccessToken;
        }

        // Try to get from cache first
        // NOTE: Cache is per-application, not per-vendor. If multiple vendors need different tokens,
        // consider using vendorCode in cache key or disabling cache (UseTokenCache=false)
        if (_cache.TryGetValue(TokenCacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            // Check if token is about to expire
            if (_cache.TryGetValue(TokenExpiryCacheKey, out DateTime cachedExpiry))
            {
                if (DateTime.UtcNow.AddMinutes(TokenExpiryBufferMinutes) < cachedExpiry)
                {
                    _logger.LogDebug("Using cached Talabat JWT token");
                    return cachedToken;
                }
                _logger.LogInformation("Talabat JWT token is about to expire, refreshing...");
            }
            else
            {
                // No expiry info, use cached token
                return cachedToken;
            }
        }

        // Need to get a new token via login
        var loginResponse = await LoginAsync(vendorCode, cancellationToken);
        
        if (string.IsNullOrEmpty(loginResponse.AccessToken))
        {
            throw new InvalidOperationException("Failed to obtain Talabat access token");
        }

        // Cache the token
        var expiresIn = loginResponse.ExpiresIn ?? 7200; // Default 2 hours
        var tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
        
        _cache.Set(TokenCacheKey, loginResponse.AccessToken, TimeSpan.FromSeconds(expiresIn));
        _cache.Set(TokenExpiryCacheKey, tokenExpiry, TimeSpan.FromSeconds(expiresIn));

        _logger.LogInformation("Talabat JWT token cached, expires at {Expiry}", tokenExpiry);

        return loginResponse.AccessToken;
    }

    /// <summary>
    /// Gets the authentication header type - V2 API uses Bearer tokens
    /// </summary>
    public string GetAuthHeaderType()
    {
        return "Bearer"; // V2 API uses JWT Bearer tokens
    }

    /// <summary>
    /// Pre-fetches and caches credentials from database BEFORE any HTTP calls.
    /// This MUST be called at the start of operations to avoid DbContext disposal issues.
    /// Safe to call multiple times - uses cache if available.
    /// </summary>
    public async Task<TalabatAccountCredentials> PreFetchCredentialsAsync(
        string? vendorCode = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CredentialsCacheKeyPrefix + (vendorCode ?? "default");

        if (_cache.TryGetValue(cacheKey, out TalabatAccountCredentials? cachedCredentials) && cachedCredentials != null)
        {
            _logger.LogDebug("Using cached Talabat credentials for VendorCode={VendorCode}", vendorCode ?? "<default>");
            return cachedCredentials;
        }

        var credentials = await _accountService.GetCredentialsWithFallbackAsync(vendorCode, cancellationToken);

        _cache.Set(cacheKey, credentials, TimeSpan.FromMinutes(CredentialsCacheMinutes));
        _logger.LogDebug("Cached Talabat credentials for VendorCode={VendorCode}, CacheDuration={Minutes}min",
            vendorCode ?? "<default>", CredentialsCacheMinutes);

        return credentials;
    }

    /// <summary>
    /// Gets credentials from cache or database.
    /// Uses cache first to avoid DbContext disposal issues during HTTP retries.
    /// </summary>
    private async Task<TalabatAccountCredentials> GetCredentialsWithCacheAsync(
        string? vendorCode = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CredentialsCacheKeyPrefix + (vendorCode ?? "default");

        if (_cache.TryGetValue(cacheKey, out TalabatAccountCredentials? cachedCredentials) && cachedCredentials != null)
        {
            _logger.LogDebug("Using cached credentials for VendorCode={VendorCode}", vendorCode ?? "<default>");
            return cachedCredentials;
        }

        try
        {
            var credentials = await _accountService.GetCredentialsWithFallbackAsync(vendorCode, cancellationToken);
            _cache.Set(cacheKey, credentials, TimeSpan.FromMinutes(CredentialsCacheMinutes));
            return credentials;
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "DbContext disposed while fetching credentials. VendorCode={VendorCode}. " +
                "Ensure PreFetchCredentialsAsync() is called before HTTP operations.", vendorCode);
            throw new InvalidOperationException(
                $"Cannot access database - context disposed. Call PreFetchCredentialsAsync() before HTTP operations. VendorCode={vendorCode}", ex);
        }
    }

    /// <summary>
    /// Performs login to get JWT access token
    /// POST /v2/login
    /// Uses form-urlencoded body with username, password, and grant_type=client_credentials
    /// Reference: Talabat V2 API documentation
    /// 
    /// FIXED: Now uses credential cache to avoid DbContext disposal during HTTP retries
    /// </summary>
    /// <param name="vendorCode">Optional vendor code to get credentials for specific TalabatAccount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<TalabatLoginResponse> LoginAsync(string? vendorCode = null, CancellationToken cancellationToken = default)
    {
        var credentials = await GetCredentialsWithCacheAsync(vendorCode, cancellationToken);

        if (!credentials.IsValid())
        {
            throw new InvalidOperationException(
                $"Talabat credentials are not configured properly. " +
                $"VendorCode={vendorCode}, UserName={credentials.UserName}, " +
                $"ChainCode={credentials.ChainCode}");
        }

        // OLD CODE (commented for reference):
        // var username = _configuration["Talabat:Username"];
        // var password = _configuration["Talabat:Password"];
        // if (string.IsNullOrWhiteSpace(username))
        // {
        //     throw new InvalidOperationException("Talabat:Username configuration is required.");
        // }
        // if (string.IsNullOrWhiteSpace(password))
        // {
        //     throw new InvalidOperationException("Talabat:Password configuration is required.");
        // }

        _logger.LogInformation(
            "Attempting Talabat login. VendorCode={VendorCode}, ChainCode={ChainCode}, Username={Username}, Source={Source}",
            credentials.VendorCode,
            credentials.ChainCode,
            credentials.UserName,
            credentials.AccountId.HasValue ? "Database" : "Configuration");
        
        _logger.LogDebug("Base URL: {BaseUrl}", _httpClient.BaseAddress);

        try
        {
            // V2 API: POST /v2/login with form-urlencoded body
            // Content-Type: application/x-www-form-urlencoded
            // Body: username=...&password=...&grant_type=client_credentials
            var endpoint = "v2/login";
            var fullUrl = new Uri(_httpClient.BaseAddress!, endpoint).ToString();
            
            _logger.LogDebug("Trying Talabat login endpoint: {Endpoint}, Full URL: {FullUrl}", endpoint, fullUrl);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            
            // Ensure no Authorization header is set (credentials are in body)
            // Also ensure no Accept header interferes (server returns JSON but we send form-urlencoded)
            // Remove any default headers that might interfere
            request.Headers.Remove("Authorization");
            if (request.Headers.Contains("Accept"))
            {
                request.Headers.Remove("Accept");
            }
            
            // Log all request headers for debugging
            _logger.LogDebug(
                "Request headers: {Headers}",
                string.Join(", ", request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
            
            // Set Content-Type to form-urlencoded (FormUrlEncodedContent sets this automatically)
            // Body: username=...&password=...&grant_type=client_credentials
            var formData = new Dictionary<string, string>
            {
                { "username", credentials.UserName! },
                { "password", credentials.Password! },
                { "grant_type", "client_credentials" }
            };
            
            request.Content = new FormUrlEncodedContent(formData);
            
            // Verify Content-Type is set correctly
            var contentType = request.Content?.Headers?.ContentType?.MediaType;
            if (contentType != "application/x-www-form-urlencoded")
            {
                _logger.LogWarning(
                    "Content-Type mismatch! Expected 'application/x-www-form-urlencoded', got '{ContentType}'",
                    contentType);
            }
            
            // Build body string for logging (without password)
            var bodyParts = new List<string>
            {
                $"username={Uri.EscapeDataString(credentials.UserName!)}",
                "password=***",
                $"grant_type={Uri.EscapeDataString(formData["grant_type"])}"
            };
            var bodyForLogging = string.Join("&", bodyParts);
            _logger.LogDebug("Login request body (sanitized): {Body}", bodyForLogging);
            
            // Log request details (without password)
            _logger.LogInformation(
                "Login request: Method={Method}, Uri={Uri}, ContentType={ContentType}, Username={Username}, GrantType={GrantType}",
                request.Method,
                fullUrl,
                contentType,
                credentials.UserName,
                formData["grant_type"]);
            
            // Don't set Accept header for login endpoint (form-urlencoded)
            // The server will return JSON response, but we don't need to specify Accept header

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Log response details
            _logger.LogDebug(
                "Login response: StatusCode={StatusCode}, ReasonPhrase={ReasonPhrase}, Headers={Headers}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Talabat login response: {ResponseBody}", responseBody);

                // Try to parse standard OAuth2 response format
                var loginResponse = JsonSerializer.Deserialize<TalabatLoginResponse>(responseBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.AccessToken))
                {
                    _logger.LogInformation(
                        "Talabat login successful via {Endpoint}. Token expires in {ExpiresIn} seconds",
                        endpoint,
                        loginResponse.ExpiresIn ?? -1);

                    return loginResponse;
                }

                // Try to parse alternative response format (token instead of access_token)
                var altResponse = JsonSerializer.Deserialize<TalabatAltLoginResponse>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (altResponse != null && !string.IsNullOrEmpty(altResponse.Token))
                {
                    _logger.LogInformation("Talabat login successful via {Endpoint} (alt format)", endpoint);
                    return new TalabatLoginResponse
                    {
                        AccessToken = altResponse.Token,
                        TokenType = "Bearer",
                        ExpiresIn = altResponse.ExpiresIn ?? 7200
                    };
                }

                _logger.LogWarning(
                    "Talabat login response did not contain access token. Response: {ResponseBody}",
                    responseBody);
                
                throw new InvalidOperationException(
                    $"Talabat login response did not contain access token. Response: {responseBody}");
            }
            else
            {
                _logger.LogError(
                    "Talabat login failed. Endpoint={Endpoint}, StatusCode={StatusCode}, Response={Response}",
                    endpoint,
                    (int)response.StatusCode,
                    responseBody);
                
                throw new HttpRequestException(
                    $"Talabat login failed with status {response.StatusCode}: {responseBody}");
            }
        }
        catch (Exception ex) when (ex is not HttpRequestException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error during Talabat login");
            throw new InvalidOperationException("Failed to login to Talabat API", ex);
        }
    }

    /// <summary>
    /// Invalidates the cached token (useful after auth errors)
    /// </summary>
    public void InvalidateToken()
    {
        _cache.Remove(TokenCacheKey);
        _cache.Remove(TokenExpiryCacheKey);
        _logger.LogInformation("Talabat JWT token cache invalidated");
    }

    /// <summary>
    /// Checks if we have a valid (non-expired) cached token
    /// </summary>
    public bool HasValidToken()
    {
        if (!_cache.TryGetValue(TokenCacheKey, out string? token) || string.IsNullOrEmpty(token))
            return false;

        if (_cache.TryGetValue(TokenExpiryCacheKey, out DateTime expiry))
        {
            return DateTime.UtcNow.AddMinutes(TokenExpiryBufferMinutes) < expiry;
        }

        return true;
    }

    private static string EnsureEndsWithSlash(string url)
    {
        return url.EndsWith("/") ? url : url + "/";
    }
}

/// <summary>
/// Alternative login response format
/// </summary>
internal class TalabatAltLoginResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("token")]
    public string? Token { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("expiresIn")]
    public int? ExpiresIn { get; set; }
}

