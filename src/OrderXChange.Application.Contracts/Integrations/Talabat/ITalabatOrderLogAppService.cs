using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

public interface ITalabatOrderLogAppService : IApplicationService
{
    Task<PagedResultDto<TalabatOrderLogDto>> GetListAsync(GetTalabatOrderLogsInput input);
    Task RetryAsync(Guid id);
}
