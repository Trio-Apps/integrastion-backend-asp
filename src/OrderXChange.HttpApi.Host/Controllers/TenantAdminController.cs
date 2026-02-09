using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OrderXChange.HttpApi.Host.Services;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace OrderXChange.HttpApi.Host.Controllers;

[Authorize(TenantManagementPermissions.Tenants.Update)]
[Route("api/tenant-admin")]
public class TenantAdminController : AbpControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IdentityUserManager _identityUserManager;
    private readonly DatabaseSmtpMailSender _smtpMailSender;
    private readonly IConfiguration _configuration;

    public TenantAdminController(
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        IdentityUserManager identityUserManager,
        DatabaseSmtpMailSender smtpMailSender,
        IConfiguration configuration)
    {
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _identityUserManager = identityUserManager;
        _smtpMailSender = smtpMailSender;
        _configuration = configuration;
    }

    [HttpPost("{tenantId:guid}/resend-welcome-email")]
    public async Task ResendWelcomeEmailAsync(Guid tenantId)
    {
        var tenant = await _tenantRepository.GetAsync(tenantId);
        var generatedPassword = GeneratePassword(tenant.Name);

        using (_currentTenant.Change(tenantId))
        {
            var adminUser = await _identityUserManager.FindByNameAsync("admin");
            if (adminUser == null || adminUser.Email.IsNullOrWhiteSpace())
            {
                throw new UserFriendlyException("Tenant admin user was not found.");
            }

            var resetToken = await _identityUserManager.GeneratePasswordResetTokenAsync(adminUser);
            var resetResult = await _identityUserManager.ResetPasswordAsync(adminUser, resetToken, generatedPassword);
            if (!resetResult.Succeeded)
            {
                throw new UserFriendlyException(string.Join("; ", resetResult.Errors.Select(e => e.Description)));
            }

            if (!adminUser.ShouldChangePasswordOnNextLogin)
            {
                adminUser.SetShouldChangePasswordOnNextLogin(true);
                var updateResult = await _identityUserManager.UpdateAsync(adminUser);
                if (!updateResult.Succeeded)
                {
                    throw new UserFriendlyException(string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                }
            }

            var angularUrl = (_configuration["App:AngularUrl"] ?? "http://localhost:4201").TrimEnd('/');
            var loginUrl = $"{angularUrl}/account/login";
            var subject = $"Welcome to OrderXChange - {tenant.Name}";
            var body = BuildWelcomeEmailHtml(tenant.Name, adminUser.Email!, generatedPassword, loginUrl);
            await _smtpMailSender.SendAsync(adminUser.Email!, subject, body);
        }
    }

    private static string GeneratePassword(string tenantName)
    {
        var prefix = new string((tenantName ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Take(4)
            .ToArray());

        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "Tenant";
        }

        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#";
        const string all = upper + lower + digits + symbols;

        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };

        for (var i = 0; i < 4; i++)
        {
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return $"{prefix}-{new string(chars.ToArray())}";
    }

    private static string BuildWelcomeEmailHtml(string tenantName, string adminEmail, string password, string loginUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
  <head>
    <meta charset=""utf-8"" />
    <title>Welcome to OrderXChange</title>
  </head>
  <body style=""margin:0; padding:0; font-family:Arial, sans-serif; background:#f5f7fb;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
      <tr>
        <td align=""center"" style=""padding:24px;"">
          <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 6px 18px rgba(0,0,0,0.08);"">
            <tr>
              <td style=""padding:28px 32px; background:#0f6d5f; color:#ffffff;"">
                <h1 style=""margin:0; font-size:20px;"">OrderXChange</h1>
                <p style=""margin:8px 0 0; font-size:14px; opacity:0.9;"">Tenant Onboarding</p>
              </td>
            </tr>
            <tr>
              <td style=""padding:28px 32px;"">
                <h2 style=""margin:0 0 12px; font-size:18px; color:#1f2937;"">Welcome, {tenantName}</h2>
                <p style=""margin:0 0 16px; color:#4b5563; font-size:14px;"">
                  Your tenant credentials were regenerated by the host admin.
                </p>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width:100%; border:1px solid #e5e7eb; border-radius:8px;"">
                  <tr>
                    <td style=""padding:12px 16px; font-size:13px; color:#6b7280; border-bottom:1px solid #e5e7eb;"">Tenant</td>
                    <td style=""padding:12px 16px; font-size:14px; color:#111827; border-bottom:1px solid #e5e7eb;"">{tenantName}</td>
                  </tr>
                  <tr>
                    <td style=""padding:12px 16px; font-size:13px; color:#6b7280; border-bottom:1px solid #e5e7eb;"">Email</td>
                    <td style=""padding:12px 16px; font-size:14px; color:#111827; border-bottom:1px solid #e5e7eb;"">{adminEmail}</td>
                  </tr>
                  <tr>
                    <td style=""padding:12px 16px; font-size:13px; color:#6b7280;"">Password</td>
                    <td style=""padding:12px 16px; font-size:14px; color:#111827;"">{password}</td>
                  </tr>
                </table>
                <div style=""margin-top:20px;"">
                  <a href=""{loginUrl}"" style=""display:inline-block; background:#10b981; color:#ffffff; text-decoration:none; padding:10px 16px; border-radius:6px; font-size:14px;"">
                    Login to OrderXChange
                  </a>
                </div>
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>";
    }
}

