using System.Threading.Tasks;

namespace OrderXChange.Emailing;

/// <summary>
/// Sends HTML mail through the host SMTP configuration.
/// Implemented by the host (DatabaseSmtpMailSender); exposed here so application-layer
/// services and background jobs can send mail without referencing the host project.
/// </summary>
public interface ISmtpMailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}
