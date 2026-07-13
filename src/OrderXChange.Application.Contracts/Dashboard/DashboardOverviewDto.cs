using System;
using System.Collections.Generic;

namespace OrderXChange.Dashboard;

/// <summary>
/// Single-call overview for the landing dashboard: order KPIs + 7-day trend,
/// menu/sync stats, setup counts and the most recent orders.
/// </summary>
public class DashboardOverviewDto
{
    public DashboardOrderStatsDto Orders { get; set; } = new();
    public List<DashboardDailyCountDto> OrdersTrend { get; set; } = new();
    public DashboardSyncStatsDto Sync { get; set; } = new();
    public DashboardSetupStatsDto Setup { get; set; } = new();
    public List<DashboardRecentOrderDto> RecentOrders { get; set; } = new();
}

public class DashboardOrderStatsDto
{
    public int Total { get; set; }
    public int Today { get; set; }
    public int Last7Days { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Processing { get; set; }
    public int Enqueued { get; set; }
    /// <summary>Succeeded / (Succeeded + Failed) * 100, rounded to 1 decimal. 0 when none finished.</summary>
    public double SuccessRate { get; set; }
    /// <summary>Sum of GrandTotal for succeeded orders in the last 7 days.</summary>
    public decimal Revenue7Days { get; set; }
}

public class DashboardDailyCountDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class DashboardSyncStatsDto
{
    public int ProductsSynced { get; set; }
    public int SubmissionsTotal { get; set; }
    public int SubmissionsSuccessful { get; set; }
    public int SubmissionsFailed { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
}

public class DashboardSetupStatsDto
{
    public int FoodicsAccounts { get; set; }
    public int TalabatVendors { get; set; }
    public int ActiveBranches { get; set; }
}

public class DashboardRecentOrderDto
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public string? CustomerName { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public decimal? GrandTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
