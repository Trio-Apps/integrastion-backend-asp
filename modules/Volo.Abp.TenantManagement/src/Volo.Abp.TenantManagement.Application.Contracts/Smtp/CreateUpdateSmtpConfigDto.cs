namespace Volo.Abp.TenantManagement.Smtp;

public class CreateUpdateSmtpConfigDto
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public bool EnableSsl { get; set; }
    public bool UseStartTls { get; set; }
}
