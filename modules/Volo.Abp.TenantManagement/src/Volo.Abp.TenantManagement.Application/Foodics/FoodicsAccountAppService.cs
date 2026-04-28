using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using OrderXChange.BackgroundJobs;
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
        private readonly IMenuSyncAppService _menuSyncAppService;

        public FoodicsAccountAppService(
            ITenantRepository tenantRepository,
            IRepository<FoodicsAccount> foodicsAccountRepository,
            IMenuSyncAppService menuSyncAppService)
        {
            _tenantRepository = tenantRepository;
            _foodicsAccountRepository = foodicsAccountRepository;
            _menuSyncAppService = menuSyncAppService;
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
                BrandName = input.BrandName,
                ApiEnvironment = FoodicsApiEnvironment.Normalize(input.ApiEnvironment)
            };
            EntityHelper.TrySetId(foodicsAccount, GuidGenerator.Create,
                           true);
            tenant.FoodicsAccounts.Add(foodicsAccount);
            await _tenantRepository.UpdateAsync(tenant , autoSave: true);

            await TriggerMenuSyncSafelyAsync(foodicsAccount.Id);

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
            foodics.ApiEnvironment = FoodicsApiEnvironment.Normalize(input.ApiEnvironment);

            //tenant.FoodicsAccounts.Add(foodics);
            await _tenantRepository.UpdateAsync(tenant, autoSave: true);

            await TriggerMenuSyncSafelyAsync(foodics.Id);
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

        public async Task<FoodicsConnectionTestResultDto> TestConnectionAsync(Guid id)
        {
            var account = await _foodicsAccountRepository.GetAsync(x => x.Id == id);

            try
            {
                var branches = await _menuSyncAppService.GetBranchesForAccountAsync(id);

                return new FoodicsConnectionTestResultDto
                {
                    Success = true,
                    Message = $"Foodics connection succeeded. Active branches returned: {branches.Count}.",
                    Details = branches.Count > 0
                        ? $"First branch: {branches[0].Name ?? branches[0].Id}"
                        : "Token was accepted, but Foodics returned no active branches.",
                    ApiEnvironment = FoodicsApiEnvironment.Normalize(account.ApiEnvironment),
                    AccessTokenConfigured = !string.IsNullOrWhiteSpace(account.AccessToken),
                    TestedAtUtc = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Foodics connection test failed for FoodicsAccountId {FoodicsAccountId}.", id);

                return new FoodicsConnectionTestResultDto
                {
                    Success = false,
                    Message = ex.Message,
                    Details = BuildExceptionDetails(ex),
                    ApiEnvironment = FoodicsApiEnvironment.Normalize(account.ApiEnvironment),
                    AccessTokenConfigured = !string.IsNullOrWhiteSpace(account.AccessToken),
                    TestedAtUtc = DateTime.UtcNow
                };
            }
        }

        private async Task TriggerMenuSyncSafelyAsync(Guid foodicsAccountId)
        {
            try
            {
                await _menuSyncAppService.TriggerMenuSyncAsync(foodicsAccountId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to trigger menu sync for FoodicsAccountId {FoodicsAccountId}.", foodicsAccountId);
            }
        }

        private static string BuildExceptionDetails(Exception exception)
        {
            var messages = new List<string>();

            for (var current = exception; current != null; current = current.InnerException)
            {
                messages.Add($"{current.GetType().Name}: {current.Message}");
            }

            return string.Join(Environment.NewLine, messages);
        }
    }
}
