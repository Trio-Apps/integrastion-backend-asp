using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsAuthClient : ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoodicsAuthClient> _logger;

    public FoodicsAuthClient(HttpClient httpClient, IConfiguration configuration, ILogger<FoodicsAuthClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<FoodicsTokenResponse> RequestAccessTokenAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        var tokenUrl = ResolveTokenUrl();
        var scope = _configuration["Foodics:OAuthScope"];

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = string.IsNullOrWhiteSpace(scope) ? null : scope
            })
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Foodics token request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(
                $"Foodics token request failed with status {(int)response.StatusCode}.");
        }

        try
        {
            var token = JsonSerializer.Deserialize<FoodicsTokenResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException("Foodics token response missing access_token.");
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Foodics token response. Body={Body}", body);
            throw;
        }
    }

    private string ResolveTokenUrl()
    {
        var explicitUrl = _configuration["Foodics:AuthUrl"] ?? _configuration["Foodics:TokenUrl"];
        if (!string.IsNullOrWhiteSpace(explicitUrl))
        {
            return explicitUrl;
        }

        var baseUrl = _configuration["Foodics:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Foodics:BaseUrl configuration is missing.");
        }

        // BaseUrl usually ends with /v5; token endpoint is typically at /oauth/token.
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/v5", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3];
        }

        return $"{trimmed}/oauth/token";
    }
}

public sealed class FoodicsTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string? TokenType { get; set; }
    public int? ExpiresIn { get; set; }
}
