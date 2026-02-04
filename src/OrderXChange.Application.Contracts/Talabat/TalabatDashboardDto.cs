using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace OrderXChange.Talabat;

public class TalabatDashboardDto
{
    public TalabatSyncCountsDto Counts { get; set; } = new();
    public List<TalabatSyncLogItemDto> RecentSubmissions { get; set; } = new();
    public TalabatBranchStatusDto BranchStatus { get; set; } = new();
    public TalabatStagingStatsDto StagingStats { get; set; } = new();
}

public class GetSyncLogsInput : PagedAndSortedResultRequestDto
{
    public string? VendorCode { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class TalabatSyncCountsDto
{
    public int TotalSubmissions { get; set; }
    public int SuccessfulSubmissions { get; set; }
    public int FailedSubmissions { get; set; }
    public int PendingSubmissions { get; set; }
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int SyncedProducts { get; set; }
}

public class TalabatSyncLogItemDto
{
    public Guid Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string? ChainCode { get; set; }
    public string? ImportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CategoriesCount { get; set; }
    public int ProductsCount { get; set; }
    public int? ProductsCreated { get; set; }
    public int? ProductsUpdated { get; set; }
    public int? ErrorsCount { get; set; }
    public string? ApiVersion { get; set; }
    public int? ProcessingDurationSeconds { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
}

public class TalabatBranchStatusDto
{
    public string VendorCode { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime? AvailableAt { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class TalabatStagingStatsDto
{
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int InactiveProducts { get; set; }
    public int SubmittedProducts { get; set; }
    public int NotSubmittedProducts { get; set; }
    public int CompletedProducts { get; set; }
    public int FailedProducts { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime? LastSubmittedAt { get; set; }
    public string? LastSyncStatus { get; set; }
}

