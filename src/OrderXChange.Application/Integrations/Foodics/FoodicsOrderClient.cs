using System;
using System.Globalization;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsOrderClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodicsOrderClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly string? _defaultAccessToken;
    private readonly JsonSerializerOptions _jsonOptions;

    public FoodicsOrderClient(HttpClient httpClient, IConfiguration configuration, ILogger<FoodicsOrderClient> logger)
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
        _httpClient.Timeout = TimeSpan.FromMinutes(2);

        _defaultAccessToken = configuration["Foodics:ApiToken"] ?? configuration["Foodics:AccessToken"];

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<FoodicsOrderResponseDto?> CreateOrderAsync(
        FoodicsOrderCreateRequest request,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        EnsureBusinessDate(request);

        _logger.LogInformation(
            "Sending Foodics create order. BranchId={BranchId}, BusinessDate={BusinessDate}, DueAt={DueAt}, ProductCount={ProductCount}",
            request.BranchId,
            request.BusinessDate,
            request.DueAt,
            request.Products?.Count ?? 0);

        var token = GetAccessToken(accessToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "orders")
        {
            Content = JsonContent.Create(request, options: _jsonOptions)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Foodics create order failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                body);
            throw new FoodicsApiException(
                response.StatusCode,
                body,
                $"Foodics create order failed with status {(int)response.StatusCode}.");
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<FoodicsSingleEnvelope<FoodicsOrderResponseDto>>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return envelope?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Foodics create order response. Body={Body}", body);
            return null;
        }
    }

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

    private static void EnsureBusinessDate(FoodicsOrderCreateRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.BusinessDate))
        {
            return;
        }

        request.BusinessDate = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string EnsureEndsWithSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
    }
}

public sealed class FoodicsApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public FoodicsApiException(HttpStatusCode statusCode, string? responseBody, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
