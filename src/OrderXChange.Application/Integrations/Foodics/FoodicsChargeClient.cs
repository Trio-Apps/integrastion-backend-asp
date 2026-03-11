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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsChargeClient : ITransientDependency
{
    private const int AmountChargeType = 1;
    private const int DeliveryOrderType = 1;

    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodicsChargeClient> _logger;
    private readonly string _baseUrl;
    private readonly string _deliveryChargeName;
    private readonly string? _deliveryChargeLocalizedName;

    public FoodicsChargeClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FoodicsChargeClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["Foodics:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Foodics:BaseUrl configuration is missing.");
        }

        _baseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _deliveryChargeName = configuration["Foodics:TalabatDeliveryChargeName"]?.Trim()
            ?? "Talabat Delivery Fee";
        _deliveryChargeLocalizedName = configuration["Foodics:TalabatDeliveryChargeLocalizedName"]?.Trim();
    }

    public async Task<string?> GetOrCreateDeliveryChargeIdAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var existingCharge = await FindDeliveryChargeAsync(accessToken, cancellationToken);
        if (existingCharge != null)
        {
            return existingCharge.Id;
        }

        try
        {
            var createdCharge = await CreateDeliveryChargeAsync(accessToken, cancellationToken);
            return createdCharge?.Id;
        }
        catch (FoodicsApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized || ex.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "Foodics token cannot create charges automatically. StatusCode={StatusCode}, ChargeName={ChargeName}",
                (int)ex.StatusCode,
                _deliveryChargeName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Foodics delivery charge automatically. ChargeName={ChargeName}", _deliveryChargeName);
            return null;
        }
    }

    private async Task<FoodicsChargeItem?> FindDeliveryChargeAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var charges = await GetChargesAsync(accessToken, cancellationToken);
        FoodicsChargeItem? nameMatch = null;

        foreach (var charge in charges)
        {
            if (string.IsNullOrWhiteSpace(charge.Name))
            {
                continue;
            }

            if (!string.Equals(charge.Name, _deliveryChargeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            nameMatch = charge;
            break;
        }

        if (nameMatch == null)
        {
            return null;
        }

        if (nameMatch.Type != AmountChargeType || !nameMatch.IsOpenCharge)
        {
            _logger.LogWarning(
                "Foodics charge with matching name exists but is incompatible. ChargeId={ChargeId}, ChargeName={ChargeName}, Type={Type}, IsOpenCharge={IsOpenCharge}",
                nameMatch.Id,
                nameMatch.Name,
                nameMatch.Type,
                nameMatch.IsOpenCharge);
            return null;
        }

        return nameMatch;
    }

    private async Task<List<FoodicsChargeItem>> GetChargesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_baseUrl), "charges?per_page=100"));
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

    private async Task<FoodicsChargeItem?> CreateDeliveryChargeAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var payload = new FoodicsChargeCreateRequest
        {
            Name = _deliveryChargeName,
            NameLocalized = string.IsNullOrWhiteSpace(_deliveryChargeLocalizedName) ? null : _deliveryChargeLocalizedName,
            Type = AmountChargeType,
            IsOpenCharge = true,
            Value = 0m,
            OrderTypes = new List<int> { DeliveryOrderType },
            AssociateToAllBranches = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_baseUrl), "charges"))
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FoodicsApiException(
                response.StatusCode,
                body,
                $"Foodics create charge failed with status {(int)response.StatusCode}.");
        }

        var result = JsonSerializer.Deserialize<FoodicsSingleEnvelope<FoodicsChargeItem>>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result?.Data == null || string.IsNullOrWhiteSpace(result.Data.Id))
        {
            throw new InvalidOperationException("Foodics create charge response is missing the created item.");
        }

        _logger.LogInformation(
            "Created Foodics delivery charge automatically. ChargeId={ChargeId}, ChargeName={ChargeName}",
            result.Data.Id,
            result.Data.Name);

        return result.Data;
    }

    private sealed class FoodicsChargeListEnvelope
    {
        [JsonPropertyName("data")]
        public List<FoodicsChargeItem>? Data { get; set; }
    }

    private sealed class FoodicsChargeCreateRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("name_localized")]
        public string? NameLocalized { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("is_open_charge")]
        public bool IsOpenCharge { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("order_types")]
        public List<int> OrderTypes { get; set; } = new();

        [JsonPropertyName("associate_to_all_branches")]
        public bool AssociateToAllBranches { get; set; }
    }

    private sealed class FoodicsChargeItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("is_open_charge")]
        public bool IsOpenCharge { get; set; }
    }
}
