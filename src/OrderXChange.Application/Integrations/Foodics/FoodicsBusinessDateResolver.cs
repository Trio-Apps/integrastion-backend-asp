using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsBusinessDateResolver : ITransientDependency
{
    private static readonly TimeSpan BranchTimezoneCacheDuration = TimeSpan.FromHours(6);

    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly FoodicsCatalogClient _foodicsCatalogClient;
    private readonly ILogger<FoodicsBusinessDateResolver> _logger;

    public FoodicsBusinessDateResolver(
        IConfiguration configuration,
        IMemoryCache cache,
        FoodicsCatalogClient foodicsCatalogClient,
        ILogger<FoodicsBusinessDateResolver> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _foodicsCatalogClient = foodicsCatalogClient;
        _logger = logger;
    }

    public async Task<BusinessDateResolutionResult> ResolveAsync(
        DateTime? orderCreatedAt,
        string vendorCode,
        string branchId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var orderTimestampUtc = NormalizeToUtc(orderCreatedAt ?? DateTime.UtcNow);

        var configuredTimezone = ResolveConfiguredTimeZone(vendorCode, branchId);
        if (!string.IsNullOrWhiteSpace(configuredTimezone))
        {
            var businessDate = ConvertToBusinessDate(orderTimestampUtc, configuredTimezone);
            return new BusinessDateResolutionResult(
                businessDate,
                configuredTimezone,
                "Config");
        }

        var branchTimezone = await ResolveBranchTimeZoneFromFoodicsAsync(
            branchId,
            accessToken,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(branchTimezone))
        {
            var businessDate = ConvertToBusinessDate(orderTimestampUtc, branchTimezone);
            return new BusinessDateResolutionResult(
                businessDate,
                branchTimezone,
                "FoodicsBranch");
        }

        return new BusinessDateResolutionResult(
            orderTimestampUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "UTC",
            "UtcFallback");
    }

    private async Task<string?> ResolveBranchTimeZoneFromFoodicsAsync(
        string branchId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branchId) || string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var cacheKey = $"foodics:branch-timezone:{branchId}";
        if (_cache.TryGetValue<string>(cacheKey, out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        try
        {
            var products = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                accessToken: accessToken,
                perPage: 100,
                includeDeleted: false,
                includeInactive: false);

            var timezone = products.Values
                .SelectMany(p => p.Branches ?? Enumerable.Empty<FoodicsBranchDto>())
                .Where(x => string.Equals(x.Id, branchId, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Timezone)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (!string.IsNullOrWhiteSpace(timezone))
            {
                _cache.Set(cacheKey, timezone, BranchTimezoneCacheDuration);
            }

            return timezone;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve Foodics branch timezone. BranchId={BranchId}",
                branchId);
            return null;
        }
    }

    private string? ResolveConfiguredTimeZone(string vendorCode, string branchId)
    {
        var branchTimezone = _configuration[$"Foodics:BusinessDateTimeZoneByBranch:{branchId}"];
        if (!string.IsNullOrWhiteSpace(branchTimezone))
        {
            return branchTimezone;
        }

        var vendorTimezone = _configuration[$"Foodics:BusinessDateTimeZoneByVendor:{vendorCode}"];
        if (!string.IsNullOrWhiteSpace(vendorTimezone))
        {
            return vendorTimezone;
        }

        return _configuration["Foodics:BusinessDateTimeZone"];
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private string ConvertToBusinessDate(DateTime utcTimestamp, string timezoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, timeZone);
            return localTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning(
                "Configured timezone was not found. TimezoneId={TimezoneId}. Falling back to UTC business date.",
                timezoneId);
        }
        catch (InvalidTimeZoneException)
        {
            _logger.LogWarning(
                "Configured timezone is invalid. TimezoneId={TimezoneId}. Falling back to UTC business date.",
                timezoneId);
        }

        return utcTimestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

public record BusinessDateResolutionResult(
    string BusinessDate,
    string? TimeZone,
    string Source);
