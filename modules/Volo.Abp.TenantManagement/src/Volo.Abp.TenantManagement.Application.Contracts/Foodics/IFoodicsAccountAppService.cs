using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Foodics
{
    public interface IFoodicsAccountAppService : IApplicationService
    {
        Task<FoodicsAccountDto> CreateAsync(CreateUpdateFoodicsAccountDto input);
        Task<FoodicsAccountDto> UpdateAsync(Guid id, CreateUpdateFoodicsAccountDto input);
        Task<PagedResultDto<FoodicsAccountDto>> GetListAsync(PagedAndSortedResultRequestDto input);
        Task DeleteAsync( Guid id);
    }
}
