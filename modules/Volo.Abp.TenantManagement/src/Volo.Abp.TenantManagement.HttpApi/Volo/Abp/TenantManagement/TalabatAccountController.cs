using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.TenantManagement.Talabat;

namespace Volo.Abp.TenantManagement;

[RemoteService(Name = TenantManagementRemoteServiceConsts.RemoteServiceName)]
[Area(TenantManagementRemoteServiceConsts.ModuleName)]
[Route("api/app/talabat-account")]
public class TalabatAccountController : AbpController, ITalabatAccountAppService
{
    private readonly ITalabatAccountAppService _talabatAccountAppService;

    public TalabatAccountController(ITalabatAccountAppService talabatAccountAppService)
    {
        _talabatAccountAppService = talabatAccountAppService;
    }

    [HttpPost]
    public virtual Task<TalabatAccountDto> CreateAsync(CreateUpdateTalabatAccountDto input)
    {
        return _talabatAccountAppService.CreateAsync(input);
    }

    [HttpPut("{id}")]
    public virtual Task<TalabatAccountDto> UpdateAsync(Guid id, CreateUpdateTalabatAccountDto input)
    {
        return _talabatAccountAppService.UpdateAsync(id, input);
    }

    [HttpGet("{id}")]
    public virtual Task<TalabatAccountDto> GetAsync(Guid id)
    {
        return _talabatAccountAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<TalabatAccountDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        return _talabatAccountAppService.GetListAsync(input);
    }

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _talabatAccountAppService.DeleteAsync(id);
    }
}

