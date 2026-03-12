using System;
using System.Threading;
using System.Threading.Tasks;
using Foodics;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsBaseUrlResolver : ITransientDependency
{
    private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public FoodicsBaseUrlResolver(
        IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _foodicsAccountRepository = foodicsAccountRepository;
        _currentTenant = currentTenant;
        _configuration = configuration;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<string> ResolveAsync(Guid? foodicsAccountId = null, CancellationToken cancellationToken = default)
    {
        var apiEnvironment = await ResolveEnvironmentAsync(foodicsAccountId, cancellationToken);

        var configuredUrl = string.Equals(apiEnvironment, FoodicsApiEnvironment.Production, StringComparison.OrdinalIgnoreCase)
            ? _configuration["Foodics:ProductionBaseUrl"]
            : _configuration["Foodics:SandboxBaseUrl"];

        configuredUrl ??= _configuration["Foodics:BaseUrl"];

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            throw new InvalidOperationException("Foodics base URL configuration is missing.");
        }

        return EnsureEndsWithSlash(configuredUrl);
    }

    private async Task<string> ResolveEnvironmentAsync(Guid? foodicsAccountId, CancellationToken cancellationToken)
    {
        if (foodicsAccountId.HasValue)
        {
            var requestedAccount = await GetAccountAsync(x => x.Id == foodicsAccountId.Value, cancellationToken);
            if (requestedAccount != null)
            {
                return FoodicsApiEnvironment.Normalize(requestedAccount.ApiEnvironment);
            }
        }

        if (_currentTenant.Id.HasValue)
        {
            var currentTenantAccount = await GetAccountAsync(x => x.TenantId == _currentTenant.Id.Value, cancellationToken);
            if (currentTenantAccount != null)
            {
                return FoodicsApiEnvironment.Normalize(currentTenantAccount.ApiEnvironment);
            }
        }

        return string.Equals(_configuration["Foodics:DefaultApiEnvironment"], FoodicsApiEnvironment.Production, StringComparison.OrdinalIgnoreCase)
            ? FoodicsApiEnvironment.Production
            : FoodicsApiEnvironment.Sandbox;
    }

    private async Task<FoodicsAccount?> GetAccountAsync(
        System.Linq.Expressions.Expression<Func<FoodicsAccount, bool>> predicate,
        CancellationToken cancellationToken)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var account = await _foodicsAccountRepository.FirstOrDefaultAsync(predicate, cancellationToken: cancellationToken);
        await uow.CompleteAsync(cancellationToken);
        return account;
    }

    private static string EnsureEndsWithSlash(string url)
    {
        return url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
    }
}
