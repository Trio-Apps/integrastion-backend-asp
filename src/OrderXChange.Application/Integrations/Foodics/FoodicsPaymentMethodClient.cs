using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsPaymentMethodClient : ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly FoodicsBaseUrlResolver _baseUrlResolver;

    public FoodicsPaymentMethodClient(HttpClient httpClient, FoodicsBaseUrlResolver baseUrlResolver)
    {
        _httpClient = httpClient;
        _baseUrlResolver = baseUrlResolver;
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<TalabatPaymentMethodDto>> GetPaymentMethodsAsync(
        string accessToken,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = await BuildUriAsync("payment_methods", foodicsAccountId, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FoodicsApiException(
                response.StatusCode,
                body,
                $"Foodics payment methods request failed with status {(int)response.StatusCode}.");
        }

        var payload = JsonSerializer.Deserialize<FoodicsPaymentMethodEnvelope>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var items = payload?.Data ?? new List<FoodicsPaymentMethodItem>();
        var result = new List<TalabatPaymentMethodDto>(items.Count);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            result.Add(MapPaymentMethod(item));
        }

        return result;
    }

    public async Task<TalabatPaymentMethodDto> CreatePaymentMethodAsync(
        string accessToken,
        string name,
        string code,
        int type,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            name,
            code,
            type
        });

        var requestUri = await BuildUriAsync("payment_methods", foodicsAccountId, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FoodicsApiException(
                response.StatusCode,
                body,
                $"Foodics create payment method failed with status {(int)response.StatusCode}.");
        }

        var result = JsonSerializer.Deserialize<FoodicsSingleEnvelope<FoodicsPaymentMethodItem>>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result?.Data == null || string.IsNullOrWhiteSpace(result.Data.Id))
        {
            throw new InvalidOperationException("Foodics create payment method response is missing the created item.");
        }

        return MapPaymentMethod(result.Data);
    }

    private async Task<Uri> BuildUriAsync(string relativePath, Guid? foodicsAccountId, CancellationToken cancellationToken)
    {
        var baseUrl = await _baseUrlResolver.ResolveAsync(foodicsAccountId, cancellationToken);
        return new Uri(new Uri(baseUrl), relativePath);
    }

    private static TalabatPaymentMethodDto MapPaymentMethod(FoodicsPaymentMethodItem item)
    {
        return new TalabatPaymentMethodDto
        {
            Id = item.Id!,
            Name = item.Name ?? item.Code ?? item.Id!,
            NameLocalized = item.NameLocalized,
            Code = item.Code,
            Type = item.Type,
            IsActive = item.IsActive
        };
    }

    private sealed class FoodicsPaymentMethodEnvelope
    {
        [JsonPropertyName("data")]
        public List<FoodicsPaymentMethodItem>? Data { get; set; }
    }

    private sealed class FoodicsPaymentMethodItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_localized")]
        public string? NameLocalized { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("type")]
        public int? Type { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }
    }
}

