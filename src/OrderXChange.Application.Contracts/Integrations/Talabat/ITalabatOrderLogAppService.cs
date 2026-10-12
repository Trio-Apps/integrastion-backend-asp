using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

public interface ITalabatOrderLogAppService : IApplicationService
{
    Task<PagedResultDto<TalabatOrderLogDto>> GetListAsync(GetTalabatOrderLogsInput input);
    Task<TalabatOrderDetailsDto> GetDetailsAsync(Guid id);
    Task RetryAsync(Guid id);
    Task<RetryTalabatOrderLogsResultDto> RetryFailedAndEnqueuedAsync(RetryTalabatOrderLogsInput input);
    Task<string> EnqueueBackfillListingFieldsAsync();
    Task<List<BranchLookupDto>> GetAccessibleBranchesAsync();

    /// <summary>Delivery platforms present in the caller's accessible branches.</summary>
    Task<List<string>> GetAggregatorsAsync();
}
