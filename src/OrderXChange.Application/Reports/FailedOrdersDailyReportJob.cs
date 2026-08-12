using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using OrderXChange.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace OrderXChange.Reports;

/// <summary>
/// Runs every 15 minutes: for each tenant that configured a report email, once the tenant-local
/// send time has passed for the day (and it hasn't sent yet today), emails the day's
/// terminally-failed orders. Dedup is tracked via a per-tenant "last sent date" setting.
/// </summary>
public class FailedOrdersDailyReportJob : ITransientDependency
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IUnitOfWorkManager _uowManager;
    private readonly FailedOrdersReportService _reportService;
    private readonly IClock _clock;
    private readonly ILogger<FailedOrdersDailyReportJob> _logger;

    public FailedOrdersDailyReportJob(
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IUnitOfWorkManager uowManager,
        FailedOrdersReportService reportService,
        IClock clock,
        ILogger<FailedOrdersDailyReportJob> logger)
    {
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _uowManager = uowManager;
        _reportService = reportService;
        _clock = clock;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task RunAsync()
    {
        var tenants = await GetTenantsAsync();
        foreach (var tenant in tenants)
        {
            try
            {
                await ProcessTenantAsync(tenant.Id, tenant.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed-orders daily report failed for tenant {TenantId} ({TenantName}).",
                    tenant.Id, tenant.Name);
            }
        }
    }

    private async Task<List<TenantInfo>> GetTenantsAsync()
    {
        using var uow = _uowManager.Begin(requiresNew: true);
        var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
        var result = tenants.Select(t => new TenantInfo(t.Id, t.Name)).ToList();
        await uow.CompleteAsync();
        return result;
    }

    private async Task ProcessTenantAsync(Guid tenantId, string tenantName)
    {
        using var uow = _uowManager.Begin(requiresNew: true);
        using (_currentTenant.Change(tenantId))
        {
            var email = (await _settingProvider.GetOrNullAsync(OrderXChangeSettings.FailedOrdersReportEmail))?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                await uow.CompleteAsync();
                return;
            }

            if (!FailedOrdersReportService.TryParseTime(
                    await _settingProvider.GetOrNullAsync(OrderXChangeSettings.FailedOrdersReportTime),
                    out var sendTime))
            {
                sendTime = new TimeSpan(23, 30, 0);
            }

            var tz = FailedOrdersReportService.ResolveTimeZone(
                await _settingProvider.GetOrNullAsync(OrderXChangeSettings.AvailabilityTimeZone));

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(NowUtc(), tz);
            var todayLabel = nowLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var lastSent = (await _settingProvider.GetOrNullAsync(OrderXChangeSettings.FailedOrdersReportLastSentDate))?.Trim();

            // Once per local day, after the configured time has passed.
            if (nowLocal.TimeOfDay >= sendTime && !string.Equals(lastSent, todayLabel, StringComparison.Ordinal))
            {
                var count = await _reportService.SendAsync(email, tz, sendIfEmpty: false);
                await _settingManager.SetForCurrentTenantAsync(
                    OrderXChangeSettings.FailedOrdersReportLastSentDate, todayLabel);

                _logger.LogInformation(
                    "Failed-orders report processed for tenant {TenantId}. Date={Date}, FailedCount={Count}, Sent={Sent}.",
                    tenantId, todayLabel, count, count > 0);
            }
        }

        await uow.CompleteAsync();
    }

    private DateTime NowUtc()
    {
        var now = _clock.Now;
        return now.Kind switch
        {
            DateTimeKind.Utc => now,
            DateTimeKind.Local => now.ToUniversalTime(),
            _ => DateTime.SpecifyKind(now, DateTimeKind.Utc)
        };
    }

    private sealed record TenantInfo(Guid Id, string Name);
}
