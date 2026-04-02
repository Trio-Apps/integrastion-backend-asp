using System;
using System.Collections.Generic;
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

    public async Task<FoodicsCustomerResponseDto?> FindCustomerAsync(
        int? dialCode,
        string? phone,
        string? email,
        string? name,
        string? accessToken = null,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var token = GetAccessToken(accessToken);
        var requestUri = await BuildCustomersSearchUriAsync(dialCode, phone, email, name, foodicsAccountId, cancellationToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Foodics customers lookup failed. StatusCode={StatusCode}, Url={Url}, Body={Body}",
                (int)response.StatusCode,
                requestUri,
                body);

            return null;
        }

        var envelope = JsonSerializer.Deserialize<FoodicsPagedEnvelope<FoodicsCustomerResponseDto>>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var customers = envelope?.Data ?? new List<FoodicsCustomerResponseDto>();
        foreach (var customer in customers)
        {
            if (IsCustomerMatch(customer, dialCode, phone, email, name))
            {
                return customer;
            }
        }

        return null;
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

    private async Task<Uri> BuildCustomersSearchUriAsync(
        int? dialCode,
        string? phone,
        string? email,
        string? name,
        Guid? foodicsAccountId,
        CancellationToken cancellationToken)
    {
        var baseUri = await BuildUriAsync("customers", foodicsAccountId, cancellationToken);
        var query = new List<string> { "page[size]=25" };

        if (dialCode.HasValue)
        {
            query.Add($"filter[dial_code]={Uri.EscapeDataString(dialCode.Value.ToString())}");
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            query.Add($"filter[phone]={Uri.EscapeDataString(phone)}");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            query.Add($"filter[email]={Uri.EscapeDataString(email)}");
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            query.Add($"filter[name]={Uri.EscapeDataString(name)}");
        }

        var builder = new UriBuilder(baseUri)
        {
            Query = string.Join("&", query)
        };

        return builder.Uri;
    }

    private static bool IsCustomerMatch(
        FoodicsCustomerResponseDto customer,
        int? dialCode,
        string? phone,
        string? email,
        string? name)
    {
        var phoneMatches = !string.IsNullOrWhiteSpace(phone) &&
                           string.Equals(customer.Phone?.Trim(), phone.Trim(), StringComparison.OrdinalIgnoreCase);
        var dialCodeMatches = !dialCode.HasValue || customer.DialCode == dialCode;
        if (phoneMatches && dialCodeMatches)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            string.Equals(customer.Email?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(name) &&
               string.Equals(customer.Name?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
