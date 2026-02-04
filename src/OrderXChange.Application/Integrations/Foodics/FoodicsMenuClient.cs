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
    private readonly IConfiguration _configuration;
    private readonly string? _defaultAccessToken;

    public FoodicsMenuClient(HttpClient httpClient, IConfiguration configuration, ILogger<FoodicsMenuClient> logger)
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

    public async Task<FoodicsMenuDisplayResponseDto> GetMenuAsync(string? branchId = null, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        var token = GetAccessToken(accessToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(branchId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogInformation("Requesting Foodics menu for branch {BranchId}", branchId ?? "<all>");

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

