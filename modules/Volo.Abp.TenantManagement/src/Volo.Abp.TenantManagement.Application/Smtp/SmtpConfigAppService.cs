using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace Volo.Abp.TenantManagement.Smtp;

[Authorize]
[RemoteService(true)]
public class SmtpConfigAppService : TenantManagementAppServiceBase, ISmtpConfigAppService
{
    private readonly IRepository<SmtpConfig, Guid> _smtpConfigRepository;

    public SmtpConfigAppService(IRepository<SmtpConfig, Guid> smtpConfigRepository)
    {
        _smtpConfigRepository = smtpConfigRepository;
    }

    public async Task<SmtpConfigDto?> GetAsync()
    {
        var tenantId = CurrentTenant.IsAvailable ? CurrentTenant.Id : null;
        var config = await _smtpConfigRepository.FindAsync(x => x.TenantId == tenantId);
        return config == null ? null : ObjectMapper.Map<SmtpConfig, SmtpConfigDto>(config);
    }

    public async Task<SmtpConfigDto> SaveAsync(CreateUpdateSmtpConfigDto input)
    {
        var tenantId = CurrentTenant.IsAvailable ? CurrentTenant.Id : null;
        var config = await _smtpConfigRepository.FindAsync(x => x.TenantId == tenantId);
        if (config == null)
        {
            config = new SmtpConfig
            {
                TenantId = tenantId
            };
        }

        config.Host = input.Host?.Trim() ?? string.Empty;
        config.Port = input.Port;
        config.UserName = input.UserName?.Trim() ?? string.Empty;
        config.Password = input.Password ?? string.Empty;
        config.FromName = input.FromName?.Trim() ?? string.Empty;
        config.FromEmail = input.FromEmail?.Trim() ?? string.Empty;
        config.EnableSsl = input.EnableSsl;
        config.UseStartTls = input.UseStartTls;

        if (config.Id == Guid.Empty)
        {
            config = await _smtpConfigRepository.InsertAsync(config, autoSave: true);
        }
        else
        {
            config = await _smtpConfigRepository.UpdateAsync(config, autoSave: true);
        }

        return ObjectMapper.Map<SmtpConfig, SmtpConfigDto>(config);
    }

    public async Task<SmtpTestResultDto> TestAsync(CreateUpdateSmtpConfigDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Host))
        {
            return new SmtpTestResultDto { Success = false, Message = "SMTP host is required." };
        }

        if (string.IsNullOrWhiteSpace(input.FromEmail))
        {
            return new SmtpTestResultDto { Success = false, Message = "From email is required." };
        }

        var host = input.Host.Trim();
        var fromEmail = input.FromEmail.Trim();
        var fromName = string.IsNullOrWhiteSpace(input.FromName) ? fromEmail : input.FromName.Trim();
        var port = input.Port > 0 ? input.Port : 587;

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = input.EnableSsl || input.UseStartTls
            };

            if (!string.IsNullOrWhiteSpace(input.UserName))
            {
                client.Credentials = new NetworkCredential(input.UserName, input.Password ?? string.Empty);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "SMTP test connection",
                Body = "SMTP test email from OrderXChange.",
                IsBodyHtml = false
            };
            message.To.Add(fromEmail);

            await client.SendMailAsync(message);

            return new SmtpTestResultDto { Success = true, Message = "Test email sent successfully." };
        }
        catch (Exception ex)
        {
            return new SmtpTestResultDto
            {
                Success = false,
                Message = $"Failed to send test email: {ex.Message}"
            };
        }
    }
}
