using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace OrderXChange.BackgroundJobs;

public interface IHangfireMonitoringAppService : IApplicationService
{
    Task<HangfireDashboardDto> GetDashboardAsync();
}

