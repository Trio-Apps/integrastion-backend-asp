using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace OrderXChange.Dashboard;

public interface IDashboardAppService : IApplicationService
{
    /// <summary>
    /// Aggregated overview for the landing dashboard (orders, sync, setup, recent orders).
    /// <paramref name="vendorCode"/> narrows every figure to one Talabat branch; null = all branches.
    /// </summary>
    Task<DashboardOverviewDto> GetOverviewAsync(string? vendorCode = null);

    /// <summary>Orders received on a single calendar day (defaults to today), optionally for one branch.</summary>
    Task<DashboardOrderCountDto> GetOrderCountAsync(DateTime? date = null, string? vendorCode = null);

    /// <summary>The Talabat branches the current user may filter the dashboard by.</summary>
    Task<List<DashboardBranchDto>> GetBranchesAsync();
}
