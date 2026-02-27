using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Settings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatPaymentMethodSettingsService : ITransientDependency
{
    private readonly FoodicsPaymentMethodClient _foodicsPaymentMethodClient;
    private readonly FoodicsAccountTokenService _foodicsAccountTokenService;
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IConfiguration _configuration;

    public TalabatPaymentMethodSettingsService(
        FoodicsPaymentMethodClient foodicsPaymentMethodClient,
        FoodicsAccountTokenService foodicsAccountTokenService,
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IConfiguration configuration)
    {
        _foodicsPaymentMethodClient = foodicsPaymentMethodClient;
        _foodicsAccountTokenService = foodicsAccountTokenService;
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _configuration = configuration;
    }

    public async Task<TalabatPaymentMethodSettingsDto> GetSettingsAsync(
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var (accessToken, source) = await ResolveAccessTokenAsync(foodicsAccountId, cancellationToken);
        var paymentMethods = await _foodicsPaymentMethodClient.GetPaymentMethodsAsync(accessToken, cancellationToken);
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

    private async Task<(string AccessToken, string Source)> ResolveAccessTokenAsync(
        Guid? foodicsAccountId,
        CancellationToken cancellationToken)
    {
        var useOverrideToken = _configuration.GetValue<bool?>("Foodics:UseOrderTestAccessToken") ?? false;
        var overrideToken = Normalize(_configuration["Foodics:OrderTestAccessToken"]);
        if (useOverrideToken && !string.IsNullOrWhiteSpace(overrideToken))
        {
            return (overrideToken, "OrderTestAccessToken");
        }

        if (foodicsAccountId.HasValue)
        {
            var accountToken = await _foodicsAccountTokenService.GetAccessTokenAsync(foodicsAccountId.Value, cancellationToken);
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

        var configToken = Normalize(_configuration["Foodics:ApiToken"])
            ?? Normalize(_configuration["Foodics:AccessToken"]);

        if (!string.IsNullOrWhiteSpace(configToken))
        {
            return (configToken, "AppSettings");
        }

        throw new UserFriendlyException(
            "No Foodics access token is available. Configure a Foodics account token or enable the order test token override.");
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
