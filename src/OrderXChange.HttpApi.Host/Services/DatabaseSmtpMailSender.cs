using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement.Smtp;

namespace OrderXChange.HttpApi.Host.Services;

public class DatabaseSmtpMailSender : ITransientDependency
{
    private readonly IRepository<SmtpConfig, Guid> _smtpConfigRepository;

    public DatabaseSmtpMailSender(IRepository<SmtpConfig, Guid> smtpConfigRepository)
    {
        _smtpConfigRepository = smtpConfigRepository;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var smtpConfig = await _smtpConfigRepository.FindAsync(x => x.TenantId == null);
        if (smtpConfig == null)
        {
            throw new UserFriendlyException("Host SMTP configuration is missing.");
        }

        if (smtpConfig.FromEmail.IsNullOrWhiteSpace())
        {
            throw new UserFriendlyException("SMTP 'From Email' is missing in host configuration.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(smtpConfig.FromEmail, smtpConfig.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port)
        {
            EnableSsl = smtpConfig.EnableSsl || smtpConfig.UseStartTls,
            Credentials = new NetworkCredential(smtpConfig.UserName, smtpConfig.Password)
        };

        await client.SendMailAsync(message);
    }
}

