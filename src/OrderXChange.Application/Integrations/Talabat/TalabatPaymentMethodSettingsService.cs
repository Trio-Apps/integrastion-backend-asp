using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Settings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.SettingManagement;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatPaymentMethodSettingsService : ITransientDependency
{
    private static readonly TimeSpan PaymentMethodsFreshCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PaymentMethodsStaleCacheDuration = TimeSpan.FromHours(2);
    private static readonly TimeSpan OrderPaymentMethodCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly Dictionary<string, SemaphoreSlim> PaymentMethodLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object PaymentMethodLocksGuard = new();

    private readonly FoodicsPaymentMethodClient _foodicsPaymentMethodClient;
    private readonly FoodicsAccountTokenService _foodicsAccountTokenService;
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TalabatPaymentMethodSettingsService> _logger;

    public TalabatPaymentMethodSettingsService(
        FoodicsPaymentMethodClient foodicsPaymentMethodClient,
        FoodicsAccountTokenService foodicsAccountTokenService,
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<TalabatPaymentMethodSettingsService> logger)
    {
        _foodicsPaymentMethodClient = foodicsPaymentMethodClient;
        _foodicsAccountTokenService = foodicsAccountTokenService;
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TalabatPaymentMethodSettingsDto> GetSettingsAsync(
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var (accessToken, source) = await ResolveAccessTokenAsync(foodicsAccountId, cancellationToken);
        var paymentMethods = await GetPaymentMethodsWithCacheAsync(accessToken, foodicsAccountId, cancellationToken);
        var activePaymentMethodId = await GetActivePaymentMethodIdAsync();
        var activePaymentMethod = paymentMethods.FirstOrDefault(x => x.Id == activePaymentMethodId);

        return new TalabatPaymentMethodSettingsDto
        {
            ActivePaymentMethodId = activePaymentMethodId,
            ActivePaymentMethodName = activePaymentMethod?.Name,
            ActivePaymentMethodCode = activePaymentMethod?.Code,
            Source = source,
            PaymentMethods = paymentMethods
        };
    }

    public async Task<TalabatPaymentMethodSettingsDto> UpdateActivePaymentMethodAsync(
        UpdateTalabatActivePaymentMethodInput input,
        CancellationToken cancellationToken = default)
    {
        var requestedPaymentMethodId = Normalize(input.PaymentMethodId);

        if (!string.IsNullOrWhiteSpace(requestedPaymentMethodId))
        {
            var current = await GetSettingsAsync(cancellationToken: cancellationToken);
            var exists = current.PaymentMethods.Any(x => x.Id == requestedPaymentMethodId);
            if (!exists)
            {
                throw new UserFriendlyException("The selected payment method was not found in Foodics.");
            }
        }

        await _settingManager.SetForCurrentTenantAsync(
            OrderXChangeSettings.TalabatActivePaymentMethodId,
            requestedPaymentMethodId ?? string.Empty);

        return await GetSettingsAsync(cancellationToken: cancellationToken);
    }

    public async Task<int> PrefetchPaymentMethodsAsync(
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(foodicsAccountId, cancellationToken);
        return settings.PaymentMethods.Count;
    }

    public async Task<string?> GetActivePaymentMethodIdAsync()
    {
        var configured = await _settingProvider.GetOrNullAsync(OrderXChangeSettings.TalabatActivePaymentMethodId);
        return Normalize(configured);
    }

    public async Task<string?> GetOrderPaymentMethodIdAsync(
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var activePaymentMethodId = await GetActivePaymentMethodIdAsync();
        if (string.IsNullOrWhiteSpace(activePaymentMethodId))
        {
            return null;
        }

        var resolvedCacheKey = BuildResolvedOrderPaymentMethodCacheKey(foodicsAccountId, activePaymentMethodId);
        if (_cache.TryGetValue<string>(resolvedCacheKey, out var cachedResolvedPaymentMethodId)
            && !string.IsNullOrWhiteSpace(cachedResolvedPaymentMethodId))
        {
            return cachedResolvedPaymentMethodId;
        }

        var (accessToken, _) = await ResolveAccessTokenAsync(foodicsAccountId, cancellationToken);
        var paymentMethods = await GetPaymentMethodsWithCacheAsync(accessToken, foodicsAccountId, cancellationToken);
        var activePaymentMethod = paymentMethods.FirstOrDefault(x => x.Id == activePaymentMethodId);

        if (activePaymentMethod == null)
        {
            return null;
        }

        if (activePaymentMethod.Type == 7)
        {
            _cache.Set(resolvedCacheKey, activePaymentMethod.Id, OrderPaymentMethodCacheDuration);
            return activePaymentMethod.Id;
        }

        var apiPaymentMethodCode = BuildApiPaymentMethodCode(activePaymentMethod);
        var existingApiPaymentMethod = paymentMethods.FirstOrDefault(x =>
            x.Type == 7 &&
            string.Equals(x.Code, apiPaymentMethodCode, StringComparison.OrdinalIgnoreCase));

        if (existingApiPaymentMethod != null)
        {
            _cache.Set(resolvedCacheKey, existingApiPaymentMethod.Id, OrderPaymentMethodCacheDuration);
            return existingApiPaymentMethod.Id;
        }

        var createdPaymentMethod = await _foodicsPaymentMethodClient.CreatePaymentMethodAsync(
            accessToken,
            BuildApiPaymentMethodName(activePaymentMethod),
            apiPaymentMethodCode,
            7,
            foodicsAccountId,
            cancellationToken);

        SetPaymentMethodsCaches(foodicsAccountId, paymentMethods.Concat([createdPaymentMethod]).ToList());
        _cache.Set(resolvedCacheKey, createdPaymentMethod.Id, OrderPaymentMethodCacheDuration);
        return createdPaymentMethod.Id;
    }

    private async Task<List<TalabatPaymentMethodDto>> GetPaymentMethodsWithCacheAsync(
        string accessToken,
        Guid? foodicsAccountId,
        CancellationToken cancellationToken)
    {
        var freshCacheKey = BuildPaymentMethodsFreshCacheKey(foodicsAccountId);
        if (_cache.TryGetValue<List<TalabatPaymentMethodDto>>(freshCacheKey, out var cachedFresh)
            && cachedFresh is { Count: > 0 })
        {
            return cachedFresh;
        }

        var paymentMethodLock = GetPaymentMethodLock(freshCacheKey);
        await paymentMethodLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue<List<TalabatPaymentMethodDto>>(freshCacheKey, out cachedFresh)
                && cachedFresh is { Count: > 0 })
            {
                return cachedFresh;
            }

            try
            {
                var paymentMethods = await _foodicsPaymentMethodClient.GetPaymentMethodsAsync(accessToken, foodicsAccountId, cancellationToken);
                SetPaymentMethodsCaches(foodicsAccountId, paymentMethods);
                return paymentMethods;
            }
            catch (FoodicsApiException ex) when ((int)ex.StatusCode == 429)
            {
                var staleCacheKey = BuildPaymentMethodsStaleCacheKey(foodicsAccountId);
                if (_cache.TryGetValue<List<TalabatPaymentMethodDto>>(staleCacheKey, out var cachedStale)
                    && cachedStale is { Count: > 0 })
                {
                    _logger.LogWarning(
                        "Foodics payment methods rate-limited for account {FoodicsAccountId}. Using stale cached payment methods count={Count}.",
                        foodicsAccountId,
                        cachedStale.Count);

                    _cache.Set(freshCacheKey, cachedStale, PaymentMethodsFreshCacheDuration);
                    return cachedStale;
                }

                throw;
            }
        }
        finally
        {
            paymentMethodLock.Release();
        }
    }

    private async Task<(string AccessToken, string Source)> ResolveAccessTokenAsync(
        Guid? foodicsAccountId,
        CancellationToken cancellationToken)
    {
        if (foodicsAccountId.HasValue)
        {
            var accountToken = await _foodicsAccountTokenService.GetAccessTokenWithFallbackAsync(foodicsAccountId.Value, cancellationToken);
            if (!string.IsNullOrWhiteSpace(accountToken))
            {
                return (accountToken, "RequestedFoodicsAccount");
            }
        }

        var tenantToken = await _foodicsAccountTokenService.GetCurrentTenantAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(tenantToken))
        {
            return (tenantToken, "CurrentTenantFoodicsAccount");
        }

        throw new UserFriendlyException(
            "No Foodics access token is available. Configure the Foodics account token or OAuth client credentials for the requested account or current tenant.");
    }

    private static string BuildApiPaymentMethodCode(TalabatPaymentMethodDto paymentMethod)
    {
        var normalizedId = new string(paymentMethod.Id.Where(char.IsLetterOrDigit).ToArray());
        if (normalizedId.Length > 12)
        {
            normalizedId = normalizedId[..12];
        }

        return "ApiPay" + normalizedId;
    }

    private static string BuildApiPaymentMethodName(TalabatPaymentMethodDto paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod.Name))
        {
            return "API Payment";
        }

        return $"API {paymentMethod.Name}";
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private void SetPaymentMethodsCaches(Guid? foodicsAccountId, List<TalabatPaymentMethodDto> paymentMethods)
    {
        _cache.Set(BuildPaymentMethodsFreshCacheKey(foodicsAccountId), paymentMethods, PaymentMethodsFreshCacheDuration);
        _cache.Set(BuildPaymentMethodsStaleCacheKey(foodicsAccountId), paymentMethods, PaymentMethodsStaleCacheDuration);
    }

    private static string BuildPaymentMethodsFreshCacheKey(Guid? foodicsAccountId)
        => $"foodics:payment-methods:fresh:{foodicsAccountId?.ToString() ?? "tenant"}";

    private static string BuildPaymentMethodsStaleCacheKey(Guid? foodicsAccountId)
        => $"foodics:payment-methods:stale:{foodicsAccountId?.ToString() ?? "tenant"}";

    private static string BuildResolvedOrderPaymentMethodCacheKey(Guid? foodicsAccountId, string activePaymentMethodId)
        => $"foodics:payment-methods:resolved:{foodicsAccountId?.ToString() ?? "tenant"}:{activePaymentMethodId}";

    private static SemaphoreSlim GetPaymentMethodLock(string cacheKey)
    {
        lock (PaymentMethodLocksGuard)
        {
            if (!PaymentMethodLocks.TryGetValue(cacheKey, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                PaymentMethodLocks[cacheKey] = semaphore;
            }

            return semaphore;
        }
    }
}

