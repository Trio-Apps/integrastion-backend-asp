using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsPaymentMethodClient : ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public FoodicsPaymentMethodClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        var baseUrl = configuration["Foodics:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Foodics:BaseUrl configuration is missing.");
        }

        _baseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<TalabatPaymentMethodDto>> GetPaymentMethodsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_baseUrl), "payment_methods"));
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

            result.Add(new TalabatPaymentMethodDto
            {
                Id = item.Id,
                Name = item.Name ?? item.Code ?? item.Id,
                NameLocalized = item.NameLocalized,
                Code = item.Code,
                Type = item.Type,
                IsActive = item.IsActive
            });
        }

        return result;
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
