using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace OrderXChange.Talabat;

public interface ITalabatDashboardAppService : IApplicationService
{
    Task<TalabatDashboardDto> GetDashboardAsync(string? vendorCode = null);
    
    Task<PagedResultDto<TalabatSyncLogItemDto>> GetSyncLogsAsync(GetSyncLogsInput input);
    
    Task<TalabatBranchStatusDto> GetBranchStatusAsync(string vendorCode);
    
    Task<TalabatBranchStatusDto> SetBranchBusyAsync(string vendorCode, string? reason = null, int? availableInMinutes = null);
    
    Task<TalabatBranchStatusDto> SetBranchAvailableAsync(string vendorCode);

    /// <summary>
    /// Returns a lookup list of Talabat vendors (from TalabatAccounts table) to drive UI selection.
    /// - Tenant users: returns only their active TalabatAccounts.
    /// - Host users: returns all active TalabatAccounts across tenants.
    /// </summary>
    Task<List<TalabatVendorLookupDto>> GetVendorsAsync();
}

