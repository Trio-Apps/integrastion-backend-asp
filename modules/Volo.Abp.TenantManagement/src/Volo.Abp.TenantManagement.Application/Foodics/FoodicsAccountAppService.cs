using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace Foodics
{
    [Authorize]
    [RemoteService(true)]
    public class FoodicsAccountAppService : TenantManagementAppServiceBase ,  IFoodicsAccountAppService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IRepository<FoodicsAccount> _foodicsAccountRepository;
        public FoodicsAccountAppService(ITenantRepository tenantRepository, IRepository<FoodicsAccount> foodicsAccountRepository)
        {
            _tenantRepository = tenantRepository;
            _foodicsAccountRepository = foodicsAccountRepository;
        }

        public  async Task<FoodicsAccountDto> CreateAsync(CreateUpdateFoodicsAccountDto input)
        {
            if (!CurrentTenant.IsAvailable)
                throw new UserFriendlyException(L["onlyTenantAvailable"]);

            var tenant = await _tenantRepository.GetAsync(CurrentTenant.Id.Value);

            var foodicsAccount = new FoodicsAccount
            {
                OAuthClientId = input.OAuthClientId,
                OAuthClientSecret = input.OAuthClientSecret,
                AccessToken = input.AccessToken,
                BrandName= input.BrandName
            };
            EntityHelper.TrySetId(foodicsAccount, GuidGenerator.Create,
                           true);
            tenant.FoodicsAccounts.Add(foodicsAccount);
            await _tenantRepository.UpdateAsync(tenant , autoSave: true);

            return ObjectMapper.Map<FoodicsAccount,FoodicsAccountDto>(foodicsAccount);
        }

        public  async Task<FoodicsAccountDto> UpdateAsync(Guid id, CreateUpdateFoodicsAccountDto input)
        {
            if (!CurrentTenant.IsAvailable)
                throw new UserFriendlyException(L["onlyTenantAvailable"]);

            var tenant = await _tenantRepository.GetAsync(CurrentTenant.Id.Value);
            var foodics = await _foodicsAccountRepository.GetAsync(x => x.Id == id);
            foodics.OAuthClientSecret = input.OAuthClientSecret;
            foodics.OAuthClientId = input.OAuthClientId;
            foodics.AccessToken = input.AccessToken;
            foodics.BrandName = input.BrandName;

            //tenant.FoodicsAccounts.Add(foodics);
            await _tenantRepository.UpdateAsync(tenant, autoSave: true);
            return ObjectMapper.Map<FoodicsAccount, FoodicsAccountDto>(foodics);
        }

        public async Task<PagedResultDto<FoodicsAccountDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var query = (await _foodicsAccountRepository.GetQueryableAsync()).PageBy(input.SkipCount, input.MaxResultCount);

            return new PagedResultDto<FoodicsAccountDto>
            {
                Items = ObjectMapper.Map<List<FoodicsAccount>, List<FoodicsAccountDto>>([.. query]),
                TotalCount = query.Count()
            };
        }

        public async Task DeleteAsync([Required]Guid id)
        {
            await _foodicsAccountRepository.DeleteAsync(x => x.Id == id);
        }
    }
}
