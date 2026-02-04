using System;
using Volo.Abp.Application.Dtos;

namespace Volo.Abp.TenantManagement.Talabat
{
    /// <summary>
    /// UPDATED: Added FoodicsAccountId to link TalabatAccount with FoodicsAccount
    /// </summary>
    public class TalabatAccountDto : FullAuditedEntityDto<Guid>
    {
        public string Name { get; set; }
        public string VendorCode { get; set; }
        public string ChainCode { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
        public bool IsActive { get; set; }
        public string UserName { get; set; }
        public string PlatformKey { get; set; }
        public string PlatformRestaurantId { get; set; }
        
        /// <summary>
        /// Link to FoodicsAccount - determines which Foodics data to sync to this Talabat vendor
        /// </summary>
        public Guid? FoodicsAccountId { get; set; }
        
        /// <summary>
        /// Display name of linked FoodicsAccount (for UI convenience)
        /// </summary>
        public string? FoodicsAccountName { get; set; }

        /// <summary>
        /// Specific Foodics branch ID to sync to this Talabat account.
        /// If null, will sync all branches based on SyncAllBranches setting.
        /// </summary>
        public string? FoodicsBranchId { get; set; }

        /// <summary>
        /// Display name of the selected Foodics branch
        /// </summary>
        public string? FoodicsBranchName { get; set; }

        /// <summary>
        /// Whether to sync all branches or only the specific branch.
        /// </summary>
        public bool SyncAllBranches { get; set; } = true;

        /// <summary>
        /// Specific Foodics group ID to sync to this Talabat account.
        /// If null, syncs all products without group filtering.
        /// </summary>
        public string? FoodicsGroupId { get; set; }

        /// <summary>
        /// Display name of the selected Foodics group
        /// </summary>
        public string? FoodicsGroupName { get; set; }
    }
}

