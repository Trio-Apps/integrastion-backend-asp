using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace OrderXChange.Dashboard;

public interface IDashboardAppService : IApplicationService
{
    /// <summary>Aggregated overview for the landing dashboard (orders, sync, setup, recent orders).</summary>
    Task<DashboardOverviewDto> GetOverviewAsync();

    /// <summary>Orders received on a single calendar day. Defaults to today.</summary>
    Task<DashboardOrderCountDto> GetOrderCountAsync(DateTime? date = null);
}
