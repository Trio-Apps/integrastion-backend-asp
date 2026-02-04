using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Foodics;

namespace OrderXChange.Application.Integrations.Foodics;

/// <summary>
/// Service to retrieve FoodicsAccount access tokens.
/// Handles token retrieval from FoodicsAccount entity with fallback to configuration.
/// 
/// FIXED: Uses IUnitOfWorkManager with requiresNew:true to avoid disposed DbContext issues
/// when called from background jobs or after tenant context changes.
/// </summary>
public class FoodicsAccountTokenService : ITransientDependency
{
	private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public FoodicsAccountTokenService(
		IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
		ICurrentTenant currentTenant,
		IUnitOfWorkManager unitOfWorkManager,
		Microsoft.Extensions.Configuration.IConfiguration configuration)
	{
		_foodicsAccountRepository = foodicsAccountRepository;
		_currentTenant = currentTenant;
		_unitOfWorkManager = unitOfWorkManager;
		_configuration = configuration;
	}

	/// <summary>
	/// Gets access token for FoodicsAccount by account ID
	/// Uses requiresNew UoW to avoid disposed DbContext issues in background jobs.
	/// </summary>
	/// <param name="foodicsAccountId">FoodicsAccount ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Access token</returns>
	/// <exception cref="InvalidOperationException">Thrown if account not found or token is missing</exception>
	public async Task<string> GetAccessTokenAsync(Guid foodicsAccountId, CancellationToken cancellationToken = default)
	{
		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		
		var account = await _foodicsAccountRepository.GetAsync(
			x => x.Id == foodicsAccountId,
			cancellationToken: cancellationToken);
		
		await uow.CompleteAsync(cancellationToken);
		
		if (account == null)
		{
			throw new InvalidOperationException($"FoodicsAccount with Id {foodicsAccountId} not found.");
		}

		if (string.IsNullOrWhiteSpace(account.AccessToken))
		{
			throw new InvalidOperationException($"Access token is not configured for FoodicsAccount {foodicsAccountId}.");
		}

		return account.AccessToken;
	}

	/// <summary>
	/// Gets access token for current tenant's FoodicsAccount
	/// Uses requiresNew UoW to avoid disposed DbContext issues in background jobs.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Access token, or null if not found</returns>
	public async Task<string?> GetCurrentTenantAccessTokenAsync(CancellationToken cancellationToken = default)
	{
		if (!_currentTenant.Id.HasValue)
		{
			return null;
		}

		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		
		var account = await _foodicsAccountRepository.FirstOrDefaultAsync(
			x => x.TenantId == _currentTenant.Id.Value,
			cancellationToken: cancellationToken);
		
		await uow.CompleteAsync(cancellationToken);

		return account?.AccessToken;
	}

	/// <summary>
	/// Gets access token with fallback priority:
	/// 1. FoodicsAccount token (if accountId provided)
	/// 2. Current tenant's FoodicsAccount token
	/// 3. Configuration token
	/// </summary>
	/// <param name="foodicsAccountId">Optional FoodicsAccount ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Access token</returns>
	/// <exception cref="InvalidOperationException">Thrown if no token found</exception>
	public async Task<string> GetAccessTokenWithFallbackAsync(Guid? foodicsAccountId = null, CancellationToken cancellationToken = default)
	{
		if (foodicsAccountId.HasValue)
		{
			var accountToken = await GetAccessTokenAsync(foodicsAccountId.Value, cancellationToken);
			if (!string.IsNullOrWhiteSpace(accountToken))
			{
				return accountToken;
			}
		}

		var tenantToken = await GetCurrentTenantAccessTokenAsync(cancellationToken);
		if (!string.IsNullOrWhiteSpace(tenantToken))
		{
			return tenantToken;
		}

		var configToken = _configuration["Foodics:ApiToken"] ?? _configuration["Foodics:AccessToken"];
		if (!string.IsNullOrWhiteSpace(configToken))
		{
			return configToken;
		}

		throw new InvalidOperationException(
			"Foodics access token not found. Configure FoodicsAccount access token or set Foodics:ApiToken/AccessToken in appsettings.");
	}
}

