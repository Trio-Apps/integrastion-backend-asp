using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsTagClient : ITransientDependency
{
    private const int OrderTagType = 4;

    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodicsTagClient> _logger;
    private readonly FoodicsBaseUrlResolver _baseUrlResolver;

    public FoodicsTagClient(
        HttpClient httpClient,
        FoodicsBaseUrlResolver baseUrlResolver,
        ILogger<FoodicsTagClient> logger)
    {
        _httpClient = httpClient;
        _baseUrlResolver = baseUrlResolver;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<FoodicsTagDto?> FindOrderTagByNameAsync(
        string tagName,
        string accessToken,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        var encodedTagName = Uri.EscapeDataString(tagName.Trim());
        var requestUri = await BuildUriAsync(
            $"tags?filter[name]={encodedTagName}&filter[type]={OrderTagType}&per_page=100",
            foodicsAccountId,
            cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FoodicsApiException(
                response.StatusCode,
                body,
                $"Foodics tags request failed with status {(int)response.StatusCode}.");
        }

        var payload = JsonSerializer.Deserialize<FoodicsTagListEnvelope>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var tags = payload?.Data ?? new List<FoodicsTagLookupItem>();
        var exactMatch = tags.FirstOrDefault(x =>
            x.Type == OrderTagType &&
            string.Equals(x.Name?.Trim(), tagName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            return new FoodicsTagDto
            {
                Id = exactMatch.Id ?? string.Empty,
                Name = exactMatch.Name,
                NameLocalized = exactMatch.NameLocalized
            };
        }

        _logger.LogInformation(
            "Foodics order tag was not found by exact name. RequestedName={TagName}, FoodicsAccountId={FoodicsAccountId}, ReturnedCount={Count}",
            tagName,
            foodicsAccountId,
            tags.Count);

        return null;
    }

    private async Task<Uri> BuildUriAsync(string relativePath, Guid? foodicsAccountId, CancellationToken cancellationToken)
    {
        var baseUrl = await _baseUrlResolver.ResolveAsync(foodicsAccountId, cancellationToken);
        return new Uri(new Uri(baseUrl), relativePath);
    }

    private sealed class FoodicsTagListEnvelope
    {
        [JsonPropertyName("data")]
        public List<FoodicsTagLookupItem>? Data { get; set; }
    }

    private sealed class FoodicsTagLookupItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_localized")]
        public string? NameLocalized { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }
    }
}
