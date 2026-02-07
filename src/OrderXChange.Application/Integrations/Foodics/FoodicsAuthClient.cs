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
        var grantType = _configuration["Foodics:OAuthGrantType"] ?? "client_credentials";
        var useBasicAuth = bool.TryParse(_configuration["Foodics:OAuthUseBasicAuth"], out var parsed) && parsed;
        var includeClientCreds = !useBasicAuth;

        HttpContent content;
        if (string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
        {
            var code = _configuration["Foodics:OAuthCode"];
            var redirectUri = _configuration["Foodics:OAuthRedirectUri"];

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException(
                    "Foodics OAuth authorization_code flow requires Foodics:OAuthCode configuration.");
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new InvalidOperationException(
                    "Foodics OAuth authorization_code flow requires Foodics:OAuthRedirectUri configuration.");
            }

            var jsonBody = new Dictionary<string, string?>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri
            };

            content = JsonContent.Create(jsonBody);
            useBasicAuth = false;
        }
        else
        {
            var form = new Dictionary<string, string?>
            {
                ["grant_type"] = grantType,
                ["scope"] = string.IsNullOrWhiteSpace(scope) ? null : scope
            };

            if (includeClientCreds)
            {
                form["client_id"] = clientId;
                form["client_secret"] = clientSecret;
            }

            content = new FormUrlEncodedContent(form);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = content
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (useBasicAuth)
        {
            var basicValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicValue);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Foodics token request failed. StatusCode={StatusCode}, Url={Url}, Body={Body}",
                (int)response.StatusCode,
                tokenUrl,
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
