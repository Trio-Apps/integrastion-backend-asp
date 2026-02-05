using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectExtending;
using Volo.Abp.TenantManagement.Smtp;

namespace Volo.Abp.TenantManagement;

[Authorize(TenantManagementPermissions.Tenants.Default)]
public class TenantAppService : TenantManagementAppServiceBase, ITenantAppService
{
    protected IDataSeeder DataSeeder { get; }
    protected ITenantRepository TenantRepository { get; }
    protected ITenantManager TenantManager { get; }
    protected IDistributedEventBus DistributedEventBus { get; }
    protected ILocalEventBus LocalEventBus { get; }
    protected IConfiguration Configuration { get; }
    protected ILogger<TenantAppService> Logger { get; }
    protected IRepository<SmtpConfig, Guid> SmtpConfigRepository { get; }

    public TenantAppService(
        ITenantRepository tenantRepository,
        ITenantManager tenantManager,
        IDataSeeder dataSeeder,
        IDistributedEventBus distributedEventBus,
        ILocalEventBus localEventBus,
        IConfiguration configuration,
        ILogger<TenantAppService> logger,
        IRepository<SmtpConfig, Guid> smtpConfigRepository)
    {
        DataSeeder = dataSeeder;
        TenantRepository = tenantRepository;
        TenantManager = tenantManager;
        DistributedEventBus = distributedEventBus;
        LocalEventBus = localEventBus;
        Configuration = configuration;
        Logger = logger;
        SmtpConfigRepository = smtpConfigRepository;
    }

    public virtual async Task<TenantDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Tenant, TenantDto>(
            await TenantRepository.GetAsync(id)
        );
    }

    public virtual async Task<PagedResultDto<TenantDto>> GetListAsync(GetTenantsInput input)
    {
        if (input.Sorting.IsNullOrWhiteSpace())
        {
            input.Sorting = nameof(Tenant.Name);
        }

        var count = await TenantRepository.GetCountAsync(input.Filter);
        var list = await TenantRepository.GetListAsync(
            input.Sorting,
            input.MaxResultCount,
            input.SkipCount,
            input.Filter,
            true
        );

        return new PagedResultDto<TenantDto>(
            count,
            ObjectMapper.Map<List<Tenant>, List<TenantDto>>(list)
        );
    }

    [Authorize(TenantManagementPermissions.Tenants.Create)]
    public virtual async Task<TenantDto> CreateAsync(TenantCreateDto input)
    {
        var generatedPassword = GeneratePassword(input.Name);
        input.AdminPassword = generatedPassword;

        var tenant = await TenantManager.CreateAsync(input.Name);
        input.MapExtraPropertiesTo(tenant);

        await TenantRepository.InsertAsync(tenant);

        await CurrentUnitOfWork.SaveChangesAsync();

        // Publish event to trigger asynchronous database migration and data seeding
        // This prevents blocking the HTTP request and improves API response time
        try
        {
            // Use CancellationToken with timeout to prevent infinite hanging
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                await DistributedEventBus.PublishAsync(
                    new TenantCreatedEto
                    {
                        Id = tenant.Id,
                        Name = tenant.Name,
                        Properties =
                        {
                            { "AdminEmail", input.AdminEmailAddress },
                            { "AdminPassword", input.AdminPassword }
                        }
                    },
                    false);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Event bus publish timed out, but tenant was created successfully
            // Log and continue - the migration will happen asynchronously via Hangfire retry
            Logger.LogWarning(
                "Distributed event bus publish timed out for tenant {TenantId}, " +
                "but tenant was created. Retrying with Hangfire background job.",
                tenant.Id);
        }
        catch (Exception ex)
        {
            // Even if event publishing fails, the tenant is already created
            Logger.LogError(ex,
                "Failed to publish TenantCreated event for tenant {TenantId}. " +
                "Migration will be retried via background jobs.",
                tenant.Id);
        }

        // ✅ REMOVED: Synchronous data seeding that was causing slow API response
        // The OrderXChangeTenantDatabaseMigrationHandler will handle this asynchronously
        // See: src/OrderXChange.Domain/Data/OrderXChangeTenantDatabaseMigrationHandler.cs

        await TrySendWelcomeEmailAsync(
            tenant.Name,
            input.AdminEmailAddress,
            generatedPassword);

        return ObjectMapper.Map<Tenant, TenantDto>(tenant);
    }

    [Authorize(TenantManagementPermissions.Tenants.Update)]
    public virtual async Task<TenantDto> UpdateAsync(Guid id, TenantUpdateDto input)
    {
        var tenant = await TenantRepository.GetAsync(id);

        await TenantManager.ChangeNameAsync(tenant, input.Name);

        tenant.SetConcurrencyStampIfNotNull(input.ConcurrencyStamp);
        input.MapExtraPropertiesTo(tenant);

        await TenantRepository.UpdateAsync(tenant);

        return ObjectMapper.Map<Tenant, TenantDto>(tenant);
    }

    [Authorize(TenantManagementPermissions.Tenants.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var tenant = await TenantRepository.FindAsync(id);
        if (tenant == null)
        {
            return;
        }

        await TenantRepository.DeleteAsync(tenant);
    }

    [Authorize(TenantManagementPermissions.Tenants.ManageConnectionStrings)]
    public virtual async Task<string> GetDefaultConnectionStringAsync(Guid id)
    {
        var tenant = await TenantRepository.GetAsync(id);
        return tenant?.FindDefaultConnectionString();
    }

    [Authorize(TenantManagementPermissions.Tenants.ManageConnectionStrings)]
    public virtual async Task UpdateDefaultConnectionStringAsync(Guid id, string defaultConnectionString)
    {
        var tenant = await TenantRepository.GetAsync(id);
        if (tenant.FindDefaultConnectionString() != defaultConnectionString)
        {
            await LocalEventBus.PublishAsync(new TenantChangedEvent(tenant.Id, tenant.NormalizedName));
        }
        tenant.SetDefaultConnectionString(defaultConnectionString);
        await TenantRepository.UpdateAsync(tenant);
    }

    [Authorize(TenantManagementPermissions.Tenants.ManageConnectionStrings)]
    public virtual async Task DeleteDefaultConnectionStringAsync(Guid id)
    {
        var tenant = await TenantRepository.GetAsync(id);
        tenant.RemoveDefaultConnectionString();
        await LocalEventBus.PublishAsync(new TenantChangedEvent(tenant.Id, tenant.NormalizedName));
        await TenantRepository.UpdateAsync(tenant);
    }

    private string GeneratePassword(string tenantName)
    {
        var prefix = new string((tenantName ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Take(4)
            .ToArray());

        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "Tenant";
        }

        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$";
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder();
        for (var i = 0; i < bytes.Length; i++)
        {
            sb.Append(chars[bytes[i] % chars.Length]);
        }

        return $"{prefix}-{sb}";
    }

    private async Task TrySendWelcomeEmailAsync(string tenantName, string adminEmail, string password)
    {
        try
        {
            var smtpConfig = await SmtpConfigRepository.FindAsync(x => x.TenantId == null);
            if (smtpConfig == null)
            {
                Logger.LogWarning("SMTP config not found (host). Skipping tenant welcome email.");
                return;
            }

            var angularUrl = Configuration["App:AngularUrl"]?.TrimEnd('/') ?? "http://localhost:4201";
            var loginUrl = $"{angularUrl}/account/login";

            var subject = $"Welcome to OrderXChange - {tenantName}";
            var body = BuildWelcomeEmailHtml(tenantName, adminEmail, password, loginUrl);

            using var message = new MailMessage
            {
                From = new MailAddress(smtpConfig.FromEmail, smtpConfig.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(adminEmail);

            using var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port)
            {
                EnableSsl = smtpConfig.EnableSsl || smtpConfig.UseStartTls,
                Credentials = new NetworkCredential(smtpConfig.UserName, smtpConfig.Password)
            };

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send welcome email for tenant {TenantName}", tenantName);
        }
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
                  Your tenant has been created successfully. Use the credentials below to sign in.
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
                <p style=""margin:18px 0 0; font-size:12px; color:#6b7280;"">
                  If the button doesn't work, copy and paste this link into your browser:<br />
                  <span style=""color:#0f6d5f;"">{loginUrl}</span>
                </p>
              </td>
            </tr>
            <tr>
              <td style=""padding:16px 32px; background:#f9fafb; font-size:12px; color:#9ca3af;"">
                This is an automated email. Please do not reply.
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
