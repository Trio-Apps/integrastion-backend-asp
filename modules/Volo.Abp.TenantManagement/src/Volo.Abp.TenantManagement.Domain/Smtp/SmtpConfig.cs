using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Volo.Abp.TenantManagement.Smtp;

public class SmtpConfig : FullAuditedEntity<Guid>, IMultiTenant
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public bool UseStartTls { get; set; } = true;

    public Guid? TenantId { get; set; }
}
