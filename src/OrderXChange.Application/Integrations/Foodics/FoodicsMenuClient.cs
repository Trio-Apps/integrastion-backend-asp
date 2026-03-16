using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsMenuClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodicsMenuClient> _logger;
    private readonly FoodicsBaseUrlResolver _baseUrlResolver;
    private readonly string? _defaultAccessToken;

    public FoodicsMenuClient(
        HttpClient httpClient,
        IConfiguration configuration,
        FoodicsBaseUrlResolver baseUrlResolver,
        ILogger<FoodicsMenuClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrlResolver = baseUrlResolver;
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Store default token from configuration as fallback (backward compatibility)
        _defaultAccessToken = configuration["Foodics:ApiToken"] ?? configuration["Foodics:AccessToken"];
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

    public async Task<FoodicsMenuDisplayResponseDto> GetMenuAsync(
        string? branchId = null,
        string? accessToken = null,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var token = GetAccessToken(accessToken);
        var requestUri = await BuildUriAsync(BuildUrl(branchId), foodicsAccountId, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogInformation(
            "Requesting Foodics menu display for branch {BranchId}, FoodicsAccountId={FoodicsAccountId}",
            branchId ?? "<all>",
            foodicsAccountId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Foodics menu request failed. StatusCode={StatusCode}, Body={Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<FoodicsMenuDisplayResponseDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("Unable to deserialize Foodics menu response.");
        }

        return payload;
    }

    private async Task<Uri> BuildUriAsync(string relativePath, Guid? foodicsAccountId, CancellationToken cancellationToken)
    {
        var baseUrl = await _baseUrlResolver.ResolveAsync(foodicsAccountId, cancellationToken);
        return new Uri(new Uri(EnsureEndsWithSlash(baseUrl)), relativePath);
    }

    private static string BuildUrl(string? branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId))
        {
            return "menu_display";
        }

        return $"menu_display?filter[branch_id]={Uri.EscapeDataString(branchId)}";
    }

    private static string EnsureEndsWithSlash(string url)
    {
        return url.EndsWith("/") ? url : url + "/";
    }
}

