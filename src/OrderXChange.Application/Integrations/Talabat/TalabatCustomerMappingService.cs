using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement.Talabat;
using Volo.Abp.Uow;
using TalabatFoodicsAddressLookupDto = OrderXChange.Application.Contracts.Integrations.Talabat.FoodicsAddressLookupDto;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatCustomerMappingService : ITransientDependency
{
    private readonly IRepository<TalabatAccount, Guid> _talabatAccountRepository;
    private readonly FoodicsAccountTokenService _foodicsAccountTokenService;
    private readonly FoodicsCustomerClient _foodicsCustomerClient;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public TalabatCustomerMappingService(
        IRepository<TalabatAccount, Guid> talabatAccountRepository,
        FoodicsAccountTokenService foodicsAccountTokenService,
        FoodicsCustomerClient foodicsCustomerClient,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _talabatAccountRepository = talabatAccountRepository;
        _foodicsAccountTokenService = foodicsAccountTokenService;
        _foodicsCustomerClient = foodicsCustomerClient;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<TalabatCustomerMappingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var queryable = await _talabatAccountRepository.GetQueryableAsync();
        var tenantId = _currentTenant.Id;

        var accounts = queryable
            .Where(x => x.IsActive && x.FoodicsAccountId != null && (!tenantId.HasValue || x.TenantId == tenantId.Value))
            .OrderBy(x => x.Name)
            .Select(x => new TalabatCustomerMappingAccountDto
            {
                TalabatAccountId = x.Id,
                Name = x.Name,
                VendorCode = x.VendorCode,
                FoodicsAccountId = x.FoodicsAccountId,
                DefaultCustomerId = x.DefaultFoodicsCustomerId,
                DefaultCustomerName = x.DefaultFoodicsCustomerName,
                DefaultCustomerAddressId = x.DefaultFoodicsCustomerAddressId,
                DefaultCustomerAddressName = x.DefaultFoodicsCustomerAddressName
            })
            .ToList();

        await uow.CompleteAsync(cancellationToken);

        return new TalabatCustomerMappingSettingsDto
        {
            Accounts = accounts
        };
    }

    public async Task<List<FoodicsCustomerLookupDto>> SearchCustomersAsync(
        Guid talabatAccountId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        var account = await GetTalabatAccountAsync(talabatAccountId, cancellationToken);
        var token = await _foodicsAccountTokenService.GetAccessTokenWithFallbackAsync(account.FoodicsAccountId!.Value, cancellationToken);
        var customers = await _foodicsCustomerClient.SearchCustomersAsync(query, token, account.FoodicsAccountId, 100, cancellationToken);

        return customers.Select(x => new FoodicsCustomerLookupDto
        {
            Id = x.Id ?? string.Empty,
            Name = x.Name,
            Phone = x.Phone,
            Email = x.Email,
            DialCode = x.DialCode
        })
        .Where(x => !string.IsNullOrWhiteSpace(x.Id))
        .ToList();
    }

    public async Task<List<TalabatFoodicsAddressLookupDto>> GetAddressesAsync(
        Guid talabatAccountId,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var account = await GetTalabatAccountAsync(talabatAccountId, cancellationToken);
        var token = await _foodicsAccountTokenService.GetAccessTokenWithFallbackAsync(account.FoodicsAccountId!.Value, cancellationToken);
        var addresses = await _foodicsCustomerClient.GetCustomerAddressesAsync(customerId, token, account.FoodicsAccountId, 100, cancellationToken);

        return addresses.Select(x => new TalabatFoodicsAddressLookupDto
        {
            Id = x.Id ?? string.Empty,
            Name = x.Name,
            Description = x.Description
        })
        .Where(x => !string.IsNullOrWhiteSpace(x.Id))
        .ToList();
    }

    public async Task<TalabatCustomerMappingSettingsDto> UpdateAsync(
        UpdateTalabatDefaultCustomerMappingInput input,
        CancellationToken cancellationToken = default)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var account = await _talabatAccountRepository.FirstOrDefaultAsync(
            x => x.Id == input.TalabatAccountId && x.IsActive,
            cancellationToken: cancellationToken);

        ValidateAccount(account);

        account.DefaultFoodicsCustomerId = Normalize(input.CustomerId);
        account.DefaultFoodicsCustomerName = Normalize(input.CustomerName);
        account.DefaultFoodicsCustomerAddressId = Normalize(input.CustomerAddressId);
        account.DefaultFoodicsCustomerAddressName = Normalize(input.CustomerAddressName);

        if (string.IsNullOrWhiteSpace(account.DefaultFoodicsCustomerId))
        {
            account.DefaultFoodicsCustomerAddressId = null;
            account.DefaultFoodicsCustomerAddressName = null;
        }
        else if (string.IsNullOrWhiteSpace(account.DefaultFoodicsCustomerAddressId))
        {
            throw new UserFriendlyException("Select a Foodics customer address before saving the mapping.");
        }

        await _talabatAccountRepository.UpdateAsync(account, autoSave: true, cancellationToken: cancellationToken);
        await uow.CompleteAsync(cancellationToken);

        return await GetSettingsAsync(cancellationToken);
    }

    private async Task<TalabatAccount> GetTalabatAccountAsync(Guid talabatAccountId, CancellationToken cancellationToken)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var account = await _talabatAccountRepository.FirstOrDefaultAsync(
            x => x.Id == talabatAccountId && x.IsActive,
            cancellationToken: cancellationToken);

        await uow.CompleteAsync(cancellationToken);

        ValidateAccount(account);
        return account;
    }

    private void ValidateAccount(TalabatAccount? account)
    {
        if (account == null)
        {
            throw new UserFriendlyException("Talabat account was not found.");
        }

        if (!account.FoodicsAccountId.HasValue)
        {
            throw new UserFriendlyException("This Talabat account is not linked to a Foodics account.");
        }

        if (_currentTenant.Id.HasValue && account.TenantId != _currentTenant.Id.Value)
        {
            throw new UserFriendlyException("Talabat account does not belong to the current tenant.");
        }
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
