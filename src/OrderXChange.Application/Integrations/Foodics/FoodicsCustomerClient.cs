using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsCustomerClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodicsCustomerClient> _logger;
    private readonly FoodicsBaseUrlResolver _baseUrlResolver;
    private readonly string? _defaultAccessToken;
    private readonly JsonSerializerOptions _jsonOptions;

    public FoodicsCustomerClient(
        HttpClient httpClient,
        IConfiguration configuration,
        FoodicsBaseUrlResolver baseUrlResolver,
        ILogger<FoodicsCustomerClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrlResolver = baseUrlResolver;
        _defaultAccessToken = configuration["Foodics:ApiToken"] ?? configuration["Foodics:AccessToken"];
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = TimeSpan.FromMinutes(2);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<FoodicsCustomerResponseDto?> CreateCustomerAsync(
        FoodicsCustomerCreateRequest request,
        string? accessToken = null,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<FoodicsCustomerCreateRequest, FoodicsCustomerResponseDto>(
            "customers",
            request,
            accessToken,
            foodicsAccountId,
            cancellationToken);
    }

    public async Task<FoodicsAddressResponseDto?> CreateAddressAsync(
        FoodicsCustomerAddressCreateRequest request,
        string? accessToken = null,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<FoodicsCustomerAddressCreateRequest, FoodicsAddressResponseDto>(
            "addresses",
            request,
            accessToken,
            foodicsAccountId,
            cancellationToken);
    }

    private async Task<TResponse?> SendAsync<TRequest, TResponse>(
        string relativePath,
        TRequest payload,
        string? accessToken,
        Guid? foodicsAccountId,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var token = GetAccessToken(accessToken);
        var requestUri = await BuildUriAsync(relativePath, foodicsAccountId, cancellationToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Foodics {Path} request failed. StatusCode={StatusCode}, Body={Body}",
                relativePath,
                (int)response.StatusCode,
                body);

            throw new FoodicsApiException(
                response.StatusCode,
                body,
                $"Foodics {relativePath} request failed with status {(int)response.StatusCode}.");
        }

        var envelope = JsonSerializer.Deserialize<FoodicsSingleEnvelope<TResponse>>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return envelope?.Data;
    }

    private string GetAccessToken(string? accessToken)
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

    private async Task<Uri> BuildUriAsync(string relativePath, Guid? foodicsAccountId, CancellationToken cancellationToken)
    {
        var baseUrl = await _baseUrlResolver.ResolveAsync(foodicsAccountId, cancellationToken);
        return new Uri(new Uri(baseUrl), relativePath);
    }
}
