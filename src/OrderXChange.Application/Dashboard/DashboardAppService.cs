using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Foodics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Authorization;
using OrderXChange.Domain.Staging;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement.Talabat;
using Volo.Abp.Timing;

namespace OrderXChange.Dashboard;

[Authorize]
public class DashboardAppService : ApplicationService, IDashboardAppService
{
    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderRepo;
    private readonly IRepository<TalabatAccount, Guid> _talabatAccountRepo;
    private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepo;
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepo;
    private readonly IRepository<TalabatCatalogSyncLog, Guid> _catalogRepo;
    private readonly ICurrentUserBranchProvider _branchProvider;
    private readonly IDataFilter _dataFilter;
    private readonly IClock _clock;

    public DashboardAppService(
        IRepository<TalabatOrderSyncLog, Guid> orderRepo,
        IRepository<TalabatAccount, Guid> talabatAccountRepo,
        IRepository<FoodicsAccount, Guid> foodicsAccountRepo,
        IRepository<FoodicsProductStaging, Guid> stagingRepo,
        IRepository<TalabatCatalogSyncLog, Guid> catalogRepo,
        ICurrentUserBranchProvider branchProvider,
        IDataFilter dataFilter,
        IClock clock)
    {
        _orderRepo = orderRepo;
        _talabatAccountRepo = talabatAccountRepo;
        _foodicsAccountRepo = foodicsAccountRepo;
        _stagingRepo = stagingRepo;
        _catalogRepo = catalogRepo;
        _branchProvider = branchProvider;
        _dataFilter = dataFilter;
        _clock = clock;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync()
    {
        // Host (no tenant) aggregates across all tenants, matching the existing dashboard.
        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable<IMultiTenant>() : null)
        {
            return await BuildOverviewAsync();
        }
    }

    private async Task<DashboardOverviewDto> BuildOverviewAsync()
    {
        var scope = await _branchProvider.GetScopeAsync();

        // Restricted users are scoped to the VendorCodes of their granted branches (fail-closed).
        List<string>? allowedVendorCodes = null;
        if (!scope.AllBranches)
        {
            if (scope.BranchIds.Count == 0)
                return new DashboardOverviewDto();

            var accQ = await _talabatAccountRepo.GetQueryableAsync();
            allowedVendorCodes = await accQ
                .Where(a => a.FoodicsBranchId != null && scope.BranchIds.Contains(a.FoodicsBranchId))
                .Select(a => a.VendorCode)
                .Distinct()
                .ToListAsync();

            if (allowedVendorCodes.Count == 0)
                return new DashboardOverviewDto();
        }

        var now = _clock.Now;
        var today = now.Date;
        var weekStart = today.AddDays(-6); // last 7 calendar days, inclusive of today

        // ── Orders ────────────────────────────────────────────────
        var orderQ = (await _orderRepo.GetQueryableAsync()).AsNoTracking();
        if (allowedVendorCodes != null)
            orderQ = orderQ.Where(x => allowedVendorCodes.Contains(x.VendorCode));

        var statusCounts = await orderQ
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountStatus(params string[] names) => statusCounts
            .Where(s => names.Any(n => string.Equals(s.Status, n, StringComparison.OrdinalIgnoreCase)))
            .Sum(s => s.Count);

        var succeeded = CountStatus("Succeeded", "Completed");
        var failed = CountStatus("Failed");
        var finished = succeeded + failed;

        var orders = new DashboardOrderStatsDto
        {
            Total = statusCounts.Sum(s => s.Count),
            Succeeded = succeeded,
            Failed = failed,
            Processing = CountStatus("Processing"),
            Enqueued = CountStatus("Enqueued", "Received"),
            Today = await orderQ.CountAsync(x => x.ReceivedAt >= today),
            Last7Days = await orderQ.CountAsync(x => x.ReceivedAt >= weekStart),
            SuccessRate = finished > 0 ? Math.Round((double)succeeded / finished * 100, 1) : 0,
            Revenue7Days = await orderQ
                .Where(x => x.ReceivedAt >= weekStart && x.GrandTotal != null
                            && (x.Status == "Succeeded" || x.Status == "Completed"))
                .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m,
        };

        // 7-day trend (bucket in memory over a bounded window)
        var recentDates = await orderQ
            .Where(x => x.ReceivedAt >= weekStart)
            .Select(x => x.ReceivedAt)
            .ToListAsync();

        var trend = Enumerable.Range(0, 7)
            .Select(i => weekStart.AddDays(i))
            .Select(d => new DashboardDailyCountDto { Date = d, Count = recentDates.Count(r => r.Date == d) })
            .ToList();

        var recentOrders = await orderQ
            .OrderByDescending(x => x.ReceivedAt)
            .Take(5)
            .Select(x => new DashboardRecentOrderDto
            {
                Id = x.Id,
                OrderCode = x.OrderCode ?? x.ShortCode,
                CustomerName = x.CustomerName,
                VendorCode = x.VendorCode,
                GrandTotal = x.GrandTotal,
                Status = x.Status,
                ReceivedAt = x.ReceivedAt,
            })
            .ToListAsync();

        // ── Sync / catalog ────────────────────────────────────────
        var stagingQ = await _stagingRepo.GetQueryableAsync();
        var catalogQ = await _catalogRepo.GetQueryableAsync();
        var lastSync = await catalogQ
            .OrderByDescending(x => x.CompletedAt ?? x.SubmittedAt)
            .Select(x => new { x.Status, x.CompletedAt, x.SubmittedAt })
            .FirstOrDefaultAsync();

        var sync = new DashboardSyncStatsDto
        {
            ProductsSynced = await stagingQ.CountAsync(x => !x.IsDeleted && x.IsActive),
            SubmissionsTotal = await catalogQ.CountAsync(),
            SubmissionsSuccessful = await catalogQ.CountAsync(x => x.Status == "Done" || x.Status == "Success"),
            SubmissionsFailed = await catalogQ.CountAsync(x => x.Status == "Failed"),
            LastSyncAt = lastSync == null ? null : (lastSync.CompletedAt ?? lastSync.SubmittedAt),
            LastSyncStatus = lastSync?.Status,
        };

        // ── Setup ─────────────────────────────────────────────────
        var vendorQ = await _talabatAccountRepo.GetQueryableAsync();
        vendorQ = vendorQ.Where(x => x.IsActive);
        if (allowedVendorCodes != null)
            vendorQ = vendorQ.Where(x => allowedVendorCodes.Contains(x.VendorCode));

        var setup = new DashboardSetupStatsDto
        {
            FoodicsAccounts = await _foodicsAccountRepo.CountAsync(),
            TalabatVendors = await vendorQ.CountAsync(),
            ActiveBranches = await vendorQ.Where(x => x.FoodicsBranchId != null)
                .Select(x => x.FoodicsBranchId).Distinct().CountAsync(),
        };

        return new DashboardOverviewDto
        {
            Orders = orders,
            OrdersTrend = trend,
            Sync = sync,
            Setup = setup,
            RecentOrders = recentOrders,
        };
    }
}
