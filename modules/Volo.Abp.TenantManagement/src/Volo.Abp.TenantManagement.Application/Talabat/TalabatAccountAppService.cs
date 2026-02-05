using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Foodics;
using Microsoft.Extensions.Logging;
using OrderXChange.BackgroundJobs;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace Volo.Abp.TenantManagement.Talabat
{
    /// <summary>
    /// UPDATED: Now handles FoodicsAccountId for linking Foodics and Talabat accounts
    /// </summary>
    [Authorize]
    [RemoteService(true)]
    public class TalabatAccountAppService : TenantManagementAppServiceBase, ITalabatAccountAppService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IRepository<TalabatAccount> _talabatAccountRepository;
        private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepository;
        private readonly IMenuSyncAppService _menuSyncAppService;

        public TalabatAccountAppService(
            ITenantRepository tenantRepository,
            IRepository<TalabatAccount> talabatAccountRepository,
            IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
            IMenuSyncAppService menuSyncAppService)
        {
            _tenantRepository = tenantRepository;
            _talabatAccountRepository = talabatAccountRepository;
            _foodicsAccountRepository = foodicsAccountRepository;
            _menuSyncAppService = menuSyncAppService;
        }

        public async Task<TalabatAccountDto> CreateAsync(CreateUpdateTalabatAccountDto input)
        {
            if (!CurrentTenant.IsAvailable)
                throw new UserFriendlyException(L["onlyTenantAvailable"]);

            var vendorCode = input.VendorCode?.Trim();
            if (string.IsNullOrWhiteSpace(vendorCode))
            {
                throw new UserFriendlyException("VendorCode is required.");
            }

            var platformRestaurantId = input.PlatformRestaurantId?.Trim();
            if (string.IsNullOrWhiteSpace(platformRestaurantId))
            {
                throw new UserFriendlyException("PlatformRestaurantId is required.");
            }

            // Enforce VendorCode uniqueness per tenant
            var vendorCodeLower = vendorCode.ToLowerInvariant();
            var exists = await _talabatAccountRepository.AnyAsync(x =>
                x.VendorCode != null && x.VendorCode.ToLower() == vendorCodeLower);
            if (exists)
            {
                throw new UserFriendlyException($"VendorCode '{vendorCode}' already exists.");
            }

            // Validate FoodicsAccountId if provided
            if (input.FoodicsAccountId.HasValue)
            {
                var foodicsAccount = await _foodicsAccountRepository.FindAsync(input.FoodicsAccountId.Value);
                if (foodicsAccount == null)
                {
                    throw new UserFriendlyException($"FoodicsAccount with Id {input.FoodicsAccountId.Value} not found.");
                }

                if (foodicsAccount.TenantId != CurrentTenant.Id)
                {
                    throw new UserFriendlyException("FoodicsAccount does not belong to the current tenant.");
                }
            }

            // Validate branch configuration
            ValidateBranchConfiguration(input);

            // ✅ Create TalabatAccount with TenantId
            var talabatAccount = new TalabatAccount
            {
                Name = input.Name,
                VendorCode = vendorCode,
                ChainCode = input.ChainCode,
                ApiKey = input.ApiKey,
                ApiSecret = input.ApiSecret,
                IsActive = input.IsActive,
                UserName = input.UserName,
                PlatformKey = input.PlatformKey,
                PlatformRestaurantId = platformRestaurantId,
                FoodicsAccountId = input.FoodicsAccountId,
                FoodicsBranchId = input.SyncAllBranches ? null : input.FoodicsBranchId,
                FoodicsBranchName = input.SyncAllBranches ? null : input.FoodicsBranchName,
                SyncAllBranches = input.SyncAllBranches,
                FoodicsGroupId = input.FoodicsGroupId,
                FoodicsGroupName = input.FoodicsGroupName,
                TenantId = CurrentTenant.Id.Value  // ✅ Important: Set TenantId explicitly
            };

            EntityHelper.TrySetId(talabatAccount, GuidGenerator.Create, true);

            // ✅ Fixed: Insert directly using repository - more reliable than Tenant collection
            await _talabatAccountRepository.InsertAsync(talabatAccount, autoSave: true);

            await TriggerMenuSyncIfLinkedAsync(talabatAccount.FoodicsAccountId);

            // ✅ Return the saved entity - no need to reload from database
            return await MapToDto(talabatAccount);
        }

        public async Task<TalabatAccountDto> UpdateAsync(Guid id, CreateUpdateTalabatAccountDto input)
        {
            if (!CurrentTenant.IsAvailable)
                throw new UserFriendlyException(L["onlyTenantAvailable"]);

            var vendorCode = input.VendorCode?.Trim();
            if (string.IsNullOrWhiteSpace(vendorCode))
            {
                throw new UserFriendlyException("VendorCode is required.");
            }

            var platformRestaurantId = input.PlatformRestaurantId?.Trim();
            if (string.IsNullOrWhiteSpace(platformRestaurantId))
            {
                throw new UserFriendlyException("PlatformRestaurantId is required.");
            }

            // Validate branch configuration
            ValidateBranchConfiguration(input);

            var talabatAccount = await _talabatAccountRepository.GetAsync(x => x.Id == id);

            // Enforce VendorCode uniqueness per tenant (excluding current entity)
            var vendorCodeLower = vendorCode.ToLowerInvariant();
            var exists = await _talabatAccountRepository.AnyAsync(x =>
                x.Id != id &&
                x.VendorCode != null &&
                x.VendorCode.ToLower() == vendorCodeLower);
            if (exists)
            {
                throw new UserFriendlyException($"VendorCode '{vendorCode}' already exists.");
            }
            
            talabatAccount.Name = input.Name;
            talabatAccount.VendorCode = vendorCode;
            talabatAccount.ChainCode = input.ChainCode;
            talabatAccount.ApiKey = input.ApiKey;
            talabatAccount.ApiSecret = input.ApiSecret;
            talabatAccount.IsActive = input.IsActive;
            talabatAccount.UserName = input.UserName;
            talabatAccount.PlatformKey = input.PlatformKey;
            talabatAccount.PlatformRestaurantId = platformRestaurantId;
            talabatAccount.FoodicsAccountId = input.FoodicsAccountId;
            talabatAccount.FoodicsBranchId = input.SyncAllBranches ? null : input.FoodicsBranchId;
            talabatAccount.FoodicsBranchName = input.SyncAllBranches ? null : input.FoodicsBranchName;
            talabatAccount.SyncAllBranches = input.SyncAllBranches;
            talabatAccount.FoodicsGroupId = input.FoodicsGroupId;
            talabatAccount.FoodicsGroupName = input.FoodicsGroupName;

            await _talabatAccountRepository.UpdateAsync(talabatAccount, autoSave: true);

            await TriggerMenuSyncIfLinkedAsync(talabatAccount.FoodicsAccountId);

            return await MapToDto(talabatAccount);
        }

        /// <summary>
        /// Validates branch configuration based on SyncAllBranches setting
        /// </summary>
        private void ValidateBranchConfiguration(CreateUpdateTalabatAccountDto input)
        {
            if (!input.SyncAllBranches && string.IsNullOrWhiteSpace(input.FoodicsBranchId))
            {
                throw new UserFriendlyException(
                    "When 'Sync All Branches' is disabled, you must select a specific Foodics branch.");
            }
        }

        public async Task<TalabatAccountDto> GetAsync(Guid id)
        {
            var talabatAccount = await _talabatAccountRepository.GetAsync(x => x.Id == id);
            return await MapToDto(talabatAccount);
        }

        public async Task<PagedResultDto<TalabatAccountDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var queryable = await _talabatAccountRepository.GetQueryableAsync();
            var totalCount = queryable.Count();
            var items = queryable
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToList();

            var dtos = new List<TalabatAccountDto>();
            foreach (var item in items)
            {
                dtos.Add(await MapToDto(item));
            }

            return new PagedResultDto<TalabatAccountDto>
            {
                Items = dtos,
                TotalCount = totalCount
            };
        }

        public async Task DeleteAsync([Required] Guid id)
        {
            await _talabatAccountRepository.DeleteAsync(x => x.Id == id);
        }

        /// <summary>
        /// Helper method to map TalabatAccount to TalabatAccountDto
        /// Includes loading FoodicsAccount name for display
        /// </summary>
        private async Task<TalabatAccountDto> MapToDto(TalabatAccount account)
        {
            var dto = ObjectMapper.Map<TalabatAccount, TalabatAccountDto>(account);

            // Load FoodicsAccount name if linked
            if (account.FoodicsAccountId.HasValue)
            {
                try
                {
                    var foodicsAccount = await _foodicsAccountRepository.FindAsync(account.FoodicsAccountId.Value);
                    if (foodicsAccount != null)
                    {
                        dto.FoodicsAccountName = foodicsAccount.BrandName ?? foodicsAccount.OAuthClientId;
                    }
                }
                catch
                {
                    // If FoodicsAccount not found, just leave null
                    dto.FoodicsAccountName = null;
                }
            }

            return dto;
        }

        private async Task TriggerMenuSyncIfLinkedAsync(Guid? foodicsAccountId)
        {
            if (!foodicsAccountId.HasValue)
            {
                return;
            }

            try
            {
                await _menuSyncAppService.TriggerMenuSyncAsync(foodicsAccountId.Value);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to trigger menu sync for FoodicsAccountId {FoodicsAccountId}.", foodicsAccountId.Value);
            }
        }
    }
}

