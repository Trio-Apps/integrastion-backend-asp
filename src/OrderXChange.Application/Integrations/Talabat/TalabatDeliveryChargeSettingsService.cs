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

public class TalabatDeliveryChargeSettingsService : ITransientDependency
{
    private readonly FoodicsChargeClient _foodicsChargeClient;
    private readonly FoodicsAccountTokenService _foodicsAccountTokenService;
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IConfiguration _configuration;

    public TalabatDeliveryChargeSettingsService(
        FoodicsChargeClient foodicsChargeClient,
        FoodicsAccountTokenService foodicsAccountTokenService,
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IConfiguration configuration)
    {
        _foodicsChargeClient = foodicsChargeClient;
        _foodicsAccountTokenService = foodicsAccountTokenService;
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _configuration = configuration;
    }

    public async Task<TalabatDeliveryChargeSettingsDto> GetSettingsAsync(
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var (accessToken, source) = await ResolveAccessTokenAsync(foodicsAccountId, cancellationToken);
        var charges = await _foodicsChargeClient.GetChargesAsync(accessToken, foodicsAccountId, cancellationToken);
        var activeDeliveryChargeId = await GetActiveDeliveryChargeIdAsync();
        var activeDeliveryCharge = charges.FirstOrDefault(x => x.Id == activeDeliveryChargeId);

        return new TalabatDeliveryChargeSettingsDto
        {
            ActiveDeliveryChargeId = activeDeliveryChargeId,
            ActiveDeliveryChargeName = activeDeliveryCharge?.Name,
            Source = source,
            Charges = charges
                .Where(IsSupportedDeliveryCharge)
                .OrderBy(x => x.Name)
                .ToList()
        };
    }

    public async Task<TalabatDeliveryChargeSettingsDto> UpdateActiveDeliveryChargeAsync(
        UpdateTalabatActiveDeliveryChargeInput input,
        CancellationToken cancellationToken = default)
    {
        var requestedChargeId = Normalize(input.DeliveryChargeId);

        if (!string.IsNullOrWhiteSpace(requestedChargeId))
        {
            var current = await GetSettingsAsync(cancellationToken: cancellationToken);
            var exists = current.Charges.Any(x => x.Id == requestedChargeId);
            if (!exists)
            {
                throw new UserFriendlyException("The selected delivery charge was not found in Foodics.");
            }
        }

        await _settingManager.SetForCurrentTenantAsync(
            OrderXChangeSettings.TalabatActiveDeliveryChargeId,
            requestedChargeId ?? string.Empty);

        return await GetSettingsAsync(cancellationToken: cancellationToken);
    }

    public async Task<string?> GetActiveDeliveryChargeIdAsync()
    {
        var configured = await _settingProvider.GetOrNullAsync(OrderXChangeSettings.TalabatActiveDeliveryChargeId);
        return Normalize(configured);
    }

    public async Task<string?> GetOrderDeliveryChargeIdAsync(
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var activeDeliveryChargeId = await GetActiveDeliveryChargeIdAsync();
        if (string.IsNullOrWhiteSpace(activeDeliveryChargeId))
        {
            return null;
        }

        var (accessToken, _) = await ResolveAccessTokenAsync(foodicsAccountId, cancellationToken);
        var charges = await _foodicsChargeClient.GetChargesAsync(accessToken, foodicsAccountId, cancellationToken);
        var activeCharge = charges.FirstOrDefault(x => x.Id == activeDeliveryChargeId);

        return activeCharge != null && IsSupportedDeliveryCharge(activeCharge)
            ? activeCharge.Id
            : null;
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

    private static bool IsSupportedDeliveryCharge(TalabatDeliveryChargeDto charge)
    {
        return charge.Type == 1 && charge.IsOpenCharge;
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
