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
using Volo.Abp.SettingManagement;

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
        var paymentMethods = await _foodicsPaymentMethodClient.GetPaymentMethodsAsync(accessToken, foodicsAccountId, cancellationToken);
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

        var (accessToken, _) = await ResolveAccessTokenAsync(foodicsAccountId, cancellationToken);
        var paymentMethods = await _foodicsPaymentMethodClient.GetPaymentMethodsAsync(accessToken, foodicsAccountId, cancellationToken);
        var activePaymentMethod = paymentMethods.FirstOrDefault(x => x.Id == activePaymentMethodId);

        if (activePaymentMethod == null)
        {
            return null;
        }

        if (activePaymentMethod.Type == 7)
        {
            return activePaymentMethod.Id;
        }

        var apiPaymentMethodCode = BuildApiPaymentMethodCode(activePaymentMethod);
        var existingApiPaymentMethod = paymentMethods.FirstOrDefault(x =>
            x.Type == 7 &&
            string.Equals(x.Code, apiPaymentMethodCode, StringComparison.OrdinalIgnoreCase));

        if (existingApiPaymentMethod != null)
        {
            return existingApiPaymentMethod.Id;
        }

        var createdPaymentMethod = await _foodicsPaymentMethodClient.CreatePaymentMethodAsync(
            accessToken,
            BuildApiPaymentMethodName(activePaymentMethod),
            apiPaymentMethodCode,
            7,
            foodicsAccountId,
            cancellationToken);

        return createdPaymentMethod.Id;
    }

    private async Task<(string AccessToken, string Source)> ResolveAccessTokenAsync(
        Guid? foodicsAccountId,
        CancellationToken cancellationToken)
    {
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

        throw new UserFriendlyException(
            "No Foodics access token is available in the database. Configure the Foodics account token for the requested account or current tenant.");
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
}

