using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace OrderXChange.HttpApi.Host.Controllers;

[AllowAnonymous]
[Route("api/account/login-resolver")]
public class AccountLoginResolverController : AbpControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantNormalizer _tenantNormalizer;
    private readonly ICurrentTenant _currentTenant;
    private readonly IdentityUserManager _identityUserManager;

    public AccountLoginResolverController(
        ITenantRepository tenantRepository,
        ITenantNormalizer tenantNormalizer,
        ICurrentTenant currentTenant,
        IdentityUserManager identityUserManager)
    {
        _tenantRepository = tenantRepository;
        _tenantNormalizer = tenantNormalizer;
        _currentTenant = currentTenant;
        _identityUserManager = identityUserManager;
    }

    [HttpGet("username")]
    public async Task<ResolvedLoginNameDto> ResolveTenantLoginNameAsync(
        [FromQuery] string tenantName,
        [FromQuery] string login)
    {
        var normalizedLogin = (login ?? string.Empty).Trim();
        if (normalizedLogin.IsNullOrWhiteSpace())
        {
            return new ResolvedLoginNameDto(normalizedLogin, false);
        }

        // If it is already a username, keep it as-is.
        if (!normalizedLogin.Contains("@", StringComparison.Ordinal))
        {
            return new ResolvedLoginNameDto(normalizedLogin, false);
        }

        if (tenantName.IsNullOrWhiteSpace())
        {
            return new ResolvedLoginNameDto(normalizedLogin, false);
        }

        var normalizedTenantName = _tenantNormalizer.NormalizeName(tenantName.Trim());
        var tenant = await _tenantRepository.FindByNameAsync(normalizedTenantName, includeDetails: false);
        if (tenant == null)
        {
            return new ResolvedLoginNameDto(normalizedLogin, false);
        }

        using (_currentTenant.Change(tenant.Id))
        {
            var user = await _identityUserManager.FindByEmailAsync(normalizedLogin);
            if (user == null || user.UserName.IsNullOrWhiteSpace())
            {
                return new ResolvedLoginNameDto(normalizedLogin, false);
            }

            return new ResolvedLoginNameDto(user.UserName, true);
        }
    }
}

public record ResolvedLoginNameDto(string Login, bool ResolvedFromEmail);
