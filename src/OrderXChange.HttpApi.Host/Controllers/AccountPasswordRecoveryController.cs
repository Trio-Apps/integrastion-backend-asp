using System;
using System.ComponentModel.DataAnnotations;
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

[AllowAnonymous]
[Route("api/account/password")]
public class AccountPasswordRecoveryController : AbpControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantNormalizer _tenantNormalizer;
    private readonly ICurrentTenant _currentTenant;
    private readonly IdentityUserManager _identityUserManager;
    private readonly DatabaseSmtpMailSender _smtpMailSender;
    private readonly IConfiguration _configuration;

    public AccountPasswordRecoveryController(
        ITenantRepository tenantRepository,
        ITenantNormalizer tenantNormalizer,
        ICurrentTenant currentTenant,
        IdentityUserManager identityUserManager,
        DatabaseSmtpMailSender smtpMailSender,
        IConfiguration configuration)
    {
        _tenantRepository = tenantRepository;
        _tenantNormalizer = tenantNormalizer;
        _currentTenant = currentTenant;
        _identityUserManager = identityUserManager;
        _smtpMailSender = smtpMailSender;
        _configuration = configuration;
    }

    [HttpPost("forgot")]
    public async Task SendResetLinkAsync([FromBody] ForgotPasswordMailInput input)
    {
        var email = (input.Email ?? string.Empty).Trim();
        if (email.IsNullOrWhiteSpace())
        {
            return;
        }

        Guid? tenantId = null;
        var tenantName = (input.TenantName ?? string.Empty).Trim();

        if (!tenantName.IsNullOrWhiteSpace())
        {
            var normalizedTenantName = _tenantNormalizer.NormalizeName(tenantName);
            var tenant = await _tenantRepository.FindByNameAsync(normalizedTenantName, includeDetails: false);
            tenantId = tenant?.Id;
        }

        using (_currentTenant.Change(tenantId))
        {
            var user = await _identityUserManager.FindByEmailAsync(email);

            if (user == null || user.Email.IsNullOrWhiteSpace())
            {
                return;
            }

            var token = await _identityUserManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = BuildResetLink(
                input.ReturnUrl,
                tenantName,
                user.Id,
                token
            );

            var body = BuildResetEmailHtml(resetLink);
            await _smtpMailSender.SendAsync(user.Email!, "Reset your OrderXChange password", body);
        }
    }

    private string BuildResetLink(string? returnUrl, string tenantName, Guid userId, string resetToken)
    {
        var baseUrl = (returnUrl ?? string.Empty).Trim();
        if (baseUrl.IsNullOrWhiteSpace())
        {
            var angularUrl = (_configuration["App:AngularUrl"] ?? "http://localhost:4201").TrimEnd('/');
            baseUrl = $"{angularUrl}/reset-password";
        }

        var delimiter = baseUrl.Contains('?') ? "&" : "?";
        var tenantQuery = tenantName.IsNullOrWhiteSpace()
            ? string.Empty
            : $"&tenantName={Uri.EscapeDataString(tenantName)}";

        return $"{baseUrl}{delimiter}userId={Uri.EscapeDataString(userId.ToString())}&resetToken={Uri.EscapeDataString(resetToken)}{tenantQuery}";
    }

    private static string BuildResetEmailHtml(string resetLink)
    {
        return $@"
<!DOCTYPE html>
<html>
  <head>
    <meta charset=""utf-8"" />
    <title>Reset Password</title>
  </head>
  <body style=""margin:0; padding:0; font-family:Arial, sans-serif; background:#f5f7fb;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
      <tr>
        <td align=""center"" style=""padding:24px;"">
          <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 6px 18px rgba(0,0,0,0.08);"">
            <tr>
              <td style=""padding:28px 32px; background:#0f6d5f; color:#ffffff;"">
                <h1 style=""margin:0; font-size:20px;"">OrderXChange</h1>
                <p style=""margin:8px 0 0; font-size:14px; opacity:0.9;"">Password Recovery</p>
              </td>
            </tr>
            <tr>
              <td style=""padding:28px 32px;"">
                <p style=""margin:0 0 16px; color:#4b5563; font-size:14px;"">
                  Click the button below to reset your password.
                </p>
                <div style=""margin-top:20px;"">
                  <a href=""{resetLink}"" style=""display:inline-block; background:#10b981; color:#ffffff; text-decoration:none; padding:10px 16px; border-radius:6px; font-size:14px;"">
                    Reset Password
                  </a>
                </div>
                <p style=""margin:18px 0 0; font-size:12px; color:#6b7280;"">
                  If the button doesn't work, copy and paste this link into your browser:<br />
                  <span style=""color:#0f6d5f;"">{resetLink}</span>
                </p>
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

public class ForgotPasswordMailInput
{
    public string? TenantName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
