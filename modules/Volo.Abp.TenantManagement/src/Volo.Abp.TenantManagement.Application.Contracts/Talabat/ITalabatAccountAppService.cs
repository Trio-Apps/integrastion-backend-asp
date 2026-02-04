using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Volo.Abp.TenantManagement.Talabat
{
    public interface ITalabatAccountAppService : IApplicationService
    {
        Task<TalabatAccountDto> CreateAsync(CreateUpdateTalabatAccountDto input);
        Task<TalabatAccountDto> UpdateAsync(Guid id, CreateUpdateTalabatAccountDto input);
        Task<TalabatAccountDto> GetAsync(Guid id);
        Task<PagedResultDto<TalabatAccountDto>> GetListAsync(PagedAndSortedResultRequestDto input);
        Task DeleteAsync(Guid id);
    }
}

