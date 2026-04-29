using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Foodics;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
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
        private readonly IDataFilter _dataFilter;

        public TalabatAccountAppService(
            ITenantRepository tenantRepository,
            IRepository<TalabatAccount> talabatAccountRepository,
            IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
            IDataFilter dataFilter)
        {
            _tenantRepository = tenantRepository;
            _talabatAccountRepository = talabatAccountRepository;
            _foodicsAccountRepository = foodicsAccountRepository;
            _dataFilter = dataFilter;
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

            await ValidateFoodicsAccountLinkAsync(input.FoodicsAccountId);

            // Validate branch configuration
            ValidateBranchConfiguration(input);

            // Enforce VendorCode uniqueness per tenant, including soft-deleted rows.
            var vendorCodeLower = vendorCode.ToLowerInvariant();
            var existingAccount = await FindAccountByVendorCodeAsync(vendorCodeLower);
            if (existingAccount != null)
            {
                if (!existingAccount.IsDeleted)
                {
                    throw new UserFriendlyException($"VendorCode '{vendorCode}' already exists.");
                }

                ApplyAccountChanges(existingAccount, input, vendorCode, platformRestaurantId);
                existingAccount.IsDeleted = false;
                existingAccount.DeleterId = null;
                existingAccount.DeletionTime = null;

                await _talabatAccountRepository.UpdateAsync(existingAccount, autoSave: true);

                return await MapToDto(existingAccount);
            }

            // Create TalabatAccount with TenantId
            var talabatAccount = new TalabatAccount
            {
                TenantId = CurrentTenant.Id.Value
            };
            ApplyAccountChanges(talabatAccount, input, vendorCode, platformRestaurantId);

            EntityHelper.TrySetId(talabatAccount, GuidGenerator.Create, true);

            await _talabatAccountRepository.InsertAsync(talabatAccount, autoSave: true);

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
            await ValidateFoodicsAccountLinkAsync(input.FoodicsAccountId);

            var talabatAccount = await _talabatAccountRepository.GetAsync(x => x.Id == id);

            // Enforce VendorCode uniqueness per tenant (excluding current entity)
            var vendorCodeLower = vendorCode.ToLowerInvariant();
            var existingAccount = await FindAccountByVendorCodeAsync(vendorCodeLower, id);
            if (existingAccount != null)
            {
                if (existingAccount.IsDeleted)
                {
                    throw new UserFriendlyException(
                        $"VendorCode '{vendorCode}' is already used by a deleted Talabat account. Delete it permanently or choose a different VendorCode.");
                }

                throw new UserFriendlyException($"VendorCode '{vendorCode}' already exists.");
            }

            ApplyAccountChanges(talabatAccount, input, vendorCode, platformRestaurantId);

            await _talabatAccountRepository.UpdateAsync(talabatAccount, autoSave: true);

            return await MapToDto(talabatAccount);
        }

        private async Task ValidateFoodicsAccountLinkAsync(Guid? foodicsAccountId)
        {
            if (!foodicsAccountId.HasValue)
            {
                return;
            }

            var foodicsAccount = await _foodicsAccountRepository.FindAsync(foodicsAccountId.Value);
            if (foodicsAccount == null)
            {
                throw new UserFriendlyException($"FoodicsAccount with Id {foodicsAccountId.Value} not found.");
            }

            if (foodicsAccount.TenantId != CurrentTenant.Id)
            {
                throw new UserFriendlyException("FoodicsAccount does not belong to the current tenant.");
            }
        }

        private async Task<TalabatAccount?> FindAccountByVendorCodeAsync(string vendorCodeLower, Guid? excludeId = null)
        {
            using (_dataFilter.Disable<ISoftDelete>())
            {
                return await _talabatAccountRepository.FirstOrDefaultAsync(x =>
                    x.VendorCode != null &&
                    x.VendorCode.ToLower() == vendorCodeLower &&
                    (!excludeId.HasValue || x.Id != excludeId.Value));
            }
        }

        private static void ApplyAccountChanges(
            TalabatAccount talabatAccount,
            CreateUpdateTalabatAccountDto input,
            string vendorCode,
            string platformRestaurantId)
        {
            talabatAccount.Name = input.Name;
            talabatAccount.VendorCode = vendorCode;
            talabatAccount.ChainCode = input.ChainCode;
            if (input.ApiKey != null)
            {
                talabatAccount.ApiKey = input.ApiKey;
            }
            if (input.ApiSecret != null)
            {
                talabatAccount.ApiSecret = input.ApiSecret;
            }
            talabatAccount.IsActive = input.IsActive;
            talabatAccount.UserName = input.UserName;
            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                talabatAccount.Password = input.Password;
            }
            talabatAccount.PlatformKey = input.PlatformKey;
            talabatAccount.PlatformRestaurantId = platformRestaurantId;
            talabatAccount.FoodicsAccountId = input.FoodicsAccountId;
            talabatAccount.FoodicsBranchId = input.SyncAllBranches ? null : input.FoodicsBranchId;
            talabatAccount.FoodicsBranchName = input.SyncAllBranches ? null : input.FoodicsBranchName;
            talabatAccount.SyncAllBranches = input.SyncAllBranches;
            talabatAccount.FoodicsGroupId = input.FoodicsGroupId;
            talabatAccount.FoodicsGroupName = input.FoodicsGroupName;
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
            if (!CurrentTenant.IsAvailable)
                throw new UserFriendlyException(L["onlyTenantAvailable"]);

            var talabatAccount = await _talabatAccountRepository.FindAsync(x => x.Id == id);
            if (talabatAccount == null)
            {
                throw new EntityNotFoundException(typeof(TalabatAccount), id);
            }

            await _talabatAccountRepository.DeleteAsync(talabatAccount);
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
    }
}
