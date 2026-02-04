using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.TenantManagement;

namespace Foodics
{
    [Controller]
    [RemoteService(Name = "FoodicsAccount")]
    [Area(TenantManagementRemoteServiceConsts.ModuleName)]
    [Route("api/foodics")]
    public class FoodicsController : AbpControllerBase, IFoodicsAccountAppService
    {
        private readonly IFoodicsAccountAppService _foodicsAccountAppService;

        public FoodicsController(IFoodicsAccountAppService foodicsAccountAppService)
        {
            _foodicsAccountAppService = foodicsAccountAppService;
        }

        [HttpPost]
        public async Task<FoodicsAccountDto> CreateAsync(CreateUpdateFoodicsAccountDto input)
        {
            return await _foodicsAccountAppService.CreateAsync(input);
        }

        [HttpDelete]
        public Task DeleteAsync(Guid id)
        {
            return _foodicsAccountAppService.DeleteAsync(id);
        }

        [HttpGet]
        public async Task<PagedResultDto<FoodicsAccountDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            return await _foodicsAccountAppService.GetListAsync(input);
        }

        [HttpPatch]
        [Route("{id}")]

        public async Task<FoodicsAccountDto> UpdateAsync(Guid id, CreateUpdateFoodicsAccountDto input)
        {
            return await _foodicsAccountAppService.UpdateAsync(id, input);
        }
    }
}
