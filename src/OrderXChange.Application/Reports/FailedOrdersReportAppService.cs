using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using OrderXChange.Permissions;
using OrderXChange.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace OrderXChange.Reports;

[Authorize(OrderXChangePermissions.DailyReport.Default)]
public class FailedOrdersReportAppService : ApplicationService, IFailedOrdersReportAppService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly FailedOrdersReportService _reportService;

    public FailedOrdersReportAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        FailedOrdersReportService reportService)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _reportService = reportService;
    }

    public async Task<FailedOrdersReportSettingsDto> GetAsync()
    {
        return new FailedOrdersReportSettingsDto
        {
            Email = await _settingProvider.GetOrNullAsync(OrderXChangeSettings.FailedOrdersReportEmail),
            Time = NormalizeTime(await _settingProvider.GetOrNullAsync(OrderXChangeSettings.FailedOrdersReportTime)),
            TimeZone = NormalizeTz(await _settingProvider.GetOrNullAsync(OrderXChangeSettings.AvailabilityTimeZone)),
        };
    }

    public async Task<FailedOrdersReportSettingsDto> UpdateAsync(UpdateFailedOrdersReportSettingsInput input)
    {
        var email = input.Email?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(email) && !IsValidEmail(email))
        {
            throw new UserFriendlyException("Enter a valid email address (or leave it empty to disable the report).");
        }

        var time = (input.Time ?? string.Empty).Trim();
        if (!FailedOrdersReportService.TryParseTime(time, out _))
        {
            throw new UserFriendlyException("Enter the send time as HH:mm (e.g. 23:30).");
        }

        await _settingManager.SetForCurrentTenantAsync(OrderXChangeSettings.FailedOrdersReportEmail, email);
        await _settingManager.SetForCurrentTenantAsync(OrderXChangeSettings.FailedOrdersReportTime, time);

        return await GetAsync();
    }

    [Authorize(OrderXChangePermissions.DailyReport.Default)]
    public async Task<FailedOrdersReportTestResultDto> SendTestAsync()
    {
        var email = (await _settingProvider.GetOrNullAsync(OrderXChangeSettings.FailedOrdersReportEmail))?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UserFriendlyException("Set and save a report email first, then send a test.");
        }

        var tz = FailedOrdersReportService.ResolveTimeZone(
            await _settingProvider.GetOrNullAsync(OrderXChangeSettings.AvailabilityTimeZone));

        var count = await _reportService.SendAsync(email, tz, sendIfEmpty: true);

        return new FailedOrdersReportTestResultDto
        {
            Sent = true,
            FailedOrderCount = count,
            Message = count == 0
                ? $"Test report sent to {email} — no failed orders today."
                : $"Test report sent to {email} with {count} failed order(s)."
        };
    }

    private static string NormalizeTime(string? v) => string.IsNullOrWhiteSpace(v) ? "23:30" : v.Trim();

    private static string NormalizeTz(string? v) => string.IsNullOrWhiteSpace(v) ? "Asia/Kuwait" : v.Trim();

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
