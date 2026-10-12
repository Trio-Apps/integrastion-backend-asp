using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace OrderXChange.Reports;

public class FailedOrdersReportSettingsDto
{
    /// <summary>Recipient email; empty means the report is disabled.</summary>
    public string? Email { get; set; }

    /// <summary>Local time of day ("HH:mm") the report is sent.</summary>
    public string Time { get; set; } = "23:30";

    /// <summary>Tenant timezone the time is interpreted in (shared with availability settings).</summary>
    public string TimeZone { get; set; } = "Asia/Kuwait";
}

public class UpdateFailedOrdersReportSettingsInput
{
    public string? Email { get; set; }
    public string Time { get; set; } = "23:30";
}

public class FailedOrdersReportTestResultDto
{
    public bool Sent { get; set; }
    public int FailedOrderCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface IFailedOrdersReportAppService : IApplicationService
{
    Task<FailedOrdersReportSettingsDto> GetAsync();
    Task<FailedOrdersReportSettingsDto> UpdateAsync(UpdateFailedOrdersReportSettingsInput input);

    /// <summary>Builds today's report and emails it now (for verifying the setup).</summary>
    Task<FailedOrdersReportTestResultDto> SendTestAsync();
}
