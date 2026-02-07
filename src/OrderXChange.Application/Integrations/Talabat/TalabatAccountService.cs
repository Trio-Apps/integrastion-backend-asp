using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.TenantManagement.Talabat;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Data;

namespace OrderXChange.Application.Integrations.Talabat;

/// <summary>
/// Service to retrieve TalabatAccount credentials and configuration for multi-tenant environment.
/// Similar to FoodicsAccountTokenService, this service provides a centralized way to get
/// Talabat account credentials with fallback to appsettings.json for backwards compatibility.
/// 
/// FIXED: Uses IUnitOfWorkManager with requiresNew:true to avoid disposed DbContext issues
/// when called from background jobs or after tenant context changes.
/// 
/// Priority order:
/// 1. TalabatAccount entity from database (by vendorCode or current tenant)
/// 2. Configuration from appsettings.json (fallback for legacy setup)
/// </summary>
public class TalabatAccountService : ITransientDependency
{
    private readonly IRepository<TalabatAccount, Guid> _talabatAccountRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatAccountService> _logger;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IDataFilter _dataFilter;

    public TalabatAccountService(
        IRepository<TalabatAccount, Guid> talabatAccountRepository,
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        IUnitOfWorkManager unitOfWorkManager,
        IDataFilter dataFilter,
        ILogger<TalabatAccountService> logger)
    {
        _talabatAccountRepository = talabatAccountRepository;
        _currentTenant = currentTenant;
        _configuration = configuration;
        _unitOfWorkManager = unitOfWorkManager;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    /// <summary>
    /// Gets TalabatAccount by vendor code
    /// Uses requiresNew UoW to avoid disposed DbContext issues in background jobs.
    /// </summary>
    /// <param name="vendorCode">Vendor code (e.g., "PH-SIDDIQ-001")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TalabatAccount or null if not found</returns>
    public async Task<TalabatAccount?> GetAccountByVendorCodeAsync(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorCode))
        {
            return null;
        }

        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        TalabatAccount? account;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            account = await _talabatAccountRepository.FirstOrDefaultAsync(
                x => x.VendorCode == vendorCode && x.IsActive,
                cancellationToken: cancellationToken);
        }
        await uow.CompleteAsync(cancellationToken);
        return account;
    }

    /// <summary>
    /// Gets TalabatAccount by PlatformRestaurantId (Talabat internal restaurant id).
    /// Uses requiresNew UoW to avoid disposed DbContext issues in background jobs.
    /// </summary>
    public async Task<TalabatAccount?> GetAccountByPlatformRestaurantIdAsync(
        string platformRestaurantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platformRestaurantId))
        {
            return null;
        }

        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        TalabatAccount? account;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            account = await _talabatAccountRepository.FirstOrDefaultAsync(
                x => x.PlatformRestaurantId == platformRestaurantId && x.IsActive,
                cancellationToken: cancellationToken);
        }
        await uow.CompleteAsync(cancellationToken);
        return account;
    }

    /// <summary>
    /// Gets TalabatAccount by ID
    /// Uses requiresNew UoW to avoid disposed DbContext issues in background jobs.
    /// </summary>
    public async Task<TalabatAccount?> GetAccountByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var account = await _talabatAccountRepository.FirstOrDefaultAsync(
            x => x.Id == accountId && x.IsActive,
            cancellationToken: cancellationToken);
        await uow.CompleteAsync(cancellationToken);
        return account;
    }

    /// <summary>
    /// Gets first active TalabatAccount for current tenant
    /// Uses requiresNew UoW to avoid disposed DbContext issues in background jobs.
    /// </summary>
    public async Task<TalabatAccount?> GetCurrentTenantAccountAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.Id.HasValue)
        {
            return null;
        }

        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var account = await _talabatAccountRepository.FirstOrDefaultAsync(
            x => x.TenantId == _currentTenant.Id.Value && x.IsActive,
            cancellationToken: cancellationToken);
        await uow.CompleteAsync(cancellationToken);
        return account;
    }

    /// <summary>
    /// Gets all active TalabatAccounts for current tenant
    /// Uses requiresNew UoW to avoid disposed DbContext issues in background jobs.
    /// </summary>
    public async Task<List<TalabatAccount>> GetCurrentTenantAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.Id.HasValue)
        {
            return new List<TalabatAccount>();
        }

        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var accounts = await _talabatAccountRepository.GetListAsync(
            x => x.TenantId == _currentTenant.Id.Value && x.IsActive,
            cancellationToken: cancellationToken);
        await uow.CompleteAsync(cancellationToken);
        return accounts;
    }

    /// <summary>
    /// Gets TalabatAccounts linked to a specific FoodicsAccount
    /// Uses requiresNew UoW to avoid disposed DbContext issues in background jobs.
    /// </summary>
    public async Task<List<TalabatAccount>> GetAccountsByFoodicsAccountIdAsync(
        Guid foodicsAccountId,
        CancellationToken cancellationToken = default)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var accounts = await _talabatAccountRepository.GetListAsync(
            x => x.FoodicsAccountId == foodicsAccountId && x.IsActive,
            cancellationToken: cancellationToken);
        await uow.CompleteAsync(cancellationToken);
        return accounts;
    }

    /// <summary>
    /// Gets Talabat credentials with fallback priority:
    /// 1. TalabatAccount from database (by vendorCode)
    /// 2. TalabatAccount from current tenant
    /// 3. Configuration from appsettings.json (legacy fallback)
    /// </summary>
    /// <param name="vendorCode">Optional vendor code to find specific account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Talabat credentials</returns>
    public async Task<TalabatAccountCredentials> GetCredentialsWithFallbackAsync(
        string? vendorCode = null,
        CancellationToken cancellationToken = default)
    {
        TalabatAccount? account = null;

        // Priority 1: Get from specific TalabatAccount by vendorCode
        if (!string.IsNullOrWhiteSpace(vendorCode))
        {
            account = await GetAccountByVendorCodeAsync(vendorCode, cancellationToken);
            if (account != null)
            {
                _logger.LogDebug(
                    "Using TalabatAccount from database. VendorCode={VendorCode}, ChainCode={ChainCode}",
                    account.VendorCode,
                    account.ChainCode);

                return MapToCredentials(account);
            }
        }

        // Priority 2: Get from current tenant's TalabatAccount
        account = await GetCurrentTenantAccountAsync(cancellationToken);
        if (account != null)
        {
            _logger.LogDebug(
                "Using TalabatAccount from current tenant. VendorCode={VendorCode}, TenantId={TenantId}",
                account.VendorCode,
                account.TenantId);

            return MapToCredentials(account);
        }

        // Priority 3: Fallback to appsettings.json (legacy support)
        _logger.LogWarning(
            "No TalabatAccount found in database. Falling back to appsettings.json configuration. " +
            "VendorCode={VendorCode}, TenantId={TenantId}",
            vendorCode,
            _currentTenant.Id);

        return GetCredentialsFromConfiguration(vendorCode);
    }

    /// <summary>
    /// Gets platform-specific configuration (PlatformKey and PlatformRestaurantId) for a vendor
    /// Uses GetAccountByVendorCodeAsync which already uses requiresNew UoW.
    /// </summary>
    public async Task<TalabatPlatformConfig?> GetPlatformConfigAsync(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        var account = await GetAccountByVendorCodeAsync(vendorCode, cancellationToken);
        if (account != null && !string.IsNullOrWhiteSpace(account.PlatformRestaurantId))
        {
            return new TalabatPlatformConfig
            {
                PlatformKey = account.PlatformKey ?? "TB",
                PlatformRestaurantId = account.PlatformRestaurantId
            };
        }

        var configKey = _configuration[$"Talabat:VendorConfig:{vendorCode}:PlatformKey"];
        var configRestaurantId = _configuration[$"Talabat:VendorConfig:{vendorCode}:PlatformRestaurantId"];

        if (!string.IsNullOrWhiteSpace(configRestaurantId))
        {
            return new TalabatPlatformConfig
            {
                PlatformKey = configKey ?? _configuration["Talabat:PlatformKey"] ?? "TB",
                PlatformRestaurantId = configRestaurantId
            };
        }

        var defaultRestaurantId = _configuration["Talabat:PlatformRestaurantId"];
        if (!string.IsNullOrWhiteSpace(defaultRestaurantId))
        {
            return new TalabatPlatformConfig
            {
                PlatformKey = _configuration["Talabat:PlatformKey"] ?? "TB",
                PlatformRestaurantId = defaultRestaurantId
            };
        }

        return null;
    }

    private TalabatAccountCredentials MapToCredentials(TalabatAccount account)
    {
        var passwordFromConfig = _configuration["Talabat:Password"];
        var password = !string.IsNullOrWhiteSpace(account.Password)
            ? account.Password
            : passwordFromConfig;
        return new TalabatAccountCredentials
        {
            AccountId = account.Id,
            Name = account.Name,
            UserName = account.UserName,
            Password = password,
            ChainCode = account.ChainCode,
            VendorCode = account.VendorCode,
            PlatformKey = account.PlatformKey,
            PlatformRestaurantId = account.PlatformRestaurantId,
            ApiKey = account.ApiKey,
            ApiSecret = account.ApiSecret,
            FoodicsAccountId = account.FoodicsAccountId
        };
    }

    private TalabatAccountCredentials GetCredentialsFromConfiguration(string? vendorCode = null)
    {
        // Get vendor-specific config or default
        var actualVendorCode = vendorCode ?? _configuration["Talabat:DefaultVendorCode"];
        
        var platformKey = _configuration[$"Talabat:VendorConfig:{actualVendorCode}:PlatformKey"]
                         ?? _configuration["Talabat:PlatformKey"]
                         ?? "TB";

        var platformRestaurantId = _configuration[$"Talabat:VendorConfig:{actualVendorCode}:PlatformRestaurantId"]
                                  ?? _configuration["Talabat:PlatformRestaurantId"];

        return new TalabatAccountCredentials
        {
            UserName = _configuration["Talabat:Username"],
            Password = _configuration["Talabat:Password"],
            ChainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick",
            VendorCode = actualVendorCode,
            PlatformKey = platformKey,
            PlatformRestaurantId = platformRestaurantId,
            ApiKey = _configuration["Talabat:ApiKey"],
            ApiSecret = _configuration["Talabat:ApiSecret"]
        };
    }
}

/// <summary>
/// Talabat account credentials and configuration
/// </summary>
public class TalabatAccountCredentials
{
    public Guid? AccountId { get; set; }
    public string? Name { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? ChainCode { get; set; }
    public string? VendorCode { get; set; }
    public string? PlatformKey { get; set; }
    public string? PlatformRestaurantId { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public Guid? FoodicsAccountId { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(UserName)
               && !string.IsNullOrWhiteSpace(Password)
               && !string.IsNullOrWhiteSpace(ChainCode);
    }
}

/// <summary>
/// Platform-specific configuration for a vendor
/// </summary>
public class TalabatPlatformConfig
{
    public string PlatformKey { get; set; } = "TB";
    public string PlatformRestaurantId { get; set; } = string.Empty;
}


