using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsChargeClient : ITransientDependency
{
    private const int AmountChargeType = 1;
    private const int DeliveryOrderType = 1;

    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodicsChargeClient> _logger;
    private readonly FoodicsBaseUrlResolver _baseUrlResolver;
    public FoodicsChargeClient(
        HttpClient httpClient,
        FoodicsBaseUrlResolver baseUrlResolver,
        ILogger<FoodicsChargeClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrlResolver = baseUrlResolver;
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    }

    public async Task<List<TalabatDeliveryChargeDto>> GetChargesAsync(
        string accessToken,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var charges = await GetChargeItemsAsync(accessToken, foodicsAccountId, cancellationToken);
        var result = new List<TalabatDeliveryChargeDto>(charges.Count);

        foreach (var charge in charges)
        {
            if (string.IsNullOrWhiteSpace(charge.Id) || string.IsNullOrWhiteSpace(charge.Name))
            {
                continue;
            }

            result.Add(new TalabatDeliveryChargeDto
            {
                Id = charge.Id,
                Name = charge.Name,
                NameLocalized = charge.NameLocalized,
                Type = charge.Type,
                IsOpenCharge = charge.IsOpenCharge,
                IsAutoApplied = charge.IsAutoApplied,
                IsCalculatedUsingSubtotal = charge.IsCalculatedUsingSubtotal,
                Value = charge.Value,
                OrderTypes = charge.OrderTypes ?? new List<int>()
            });
        }

        return result;
    }

    private async Task<List<FoodicsChargeItem>> GetChargeItemsAsync(
        string accessToken,
        Guid? foodicsAccountId,
        CancellationToken cancellationToken)
    {
        var requestUri = await BuildUriAsync("charges?per_page=100", foodicsAccountId, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FoodicsApiException(
                response.StatusCode,
                body,
                $"Foodics charges request failed with status {(int)response.StatusCode}.");
        }

        var payload = JsonSerializer.Deserialize<FoodicsChargeListEnvelope>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return payload?.Data ?? new List<FoodicsChargeItem>();
    }

    private async Task<Uri> BuildUriAsync(string relativePath, Guid? foodicsAccountId, CancellationToken cancellationToken)
    {
        var baseUrl = await _baseUrlResolver.ResolveAsync(foodicsAccountId, cancellationToken);
        return new Uri(new Uri(baseUrl), relativePath);
    }

    private sealed class FoodicsChargeListEnvelope
    {
        [JsonPropertyName("data")]
        public List<FoodicsChargeItem>? Data { get; set; }
    }

    private sealed class FoodicsChargeItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_localized")]
        public string? NameLocalized { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("is_open_charge")]
        public bool IsOpenCharge { get; set; }

        [JsonPropertyName("is_auto_applied")]
        public bool IsAutoApplied { get; set; }

        [JsonPropertyName("is_calculated_using_subtotal")]
        public bool IsCalculatedUsingSubtotal { get; set; }

        [JsonPropertyName("value")]
        public decimal? Value { get; set; }

        [JsonPropertyName("order_types")]
        public List<int>? OrderTypes { get; set; }
    }
}
