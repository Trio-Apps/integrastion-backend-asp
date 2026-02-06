using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Volo.Abp.TenantManagement.Talabat
{
    /// <summary>
    /// UPDATED: Added FoodicsAccountId to link TalabatAccount with FoodicsAccount
    /// </summary>
    public class CreateUpdateTalabatAccountDto : EntityDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string VendorCode { get; set; }

        [StringLength(50)]
        public string ChainCode { get; set; }

        [StringLength(200)]
        public string ApiKey { get; set; }

        [StringLength(200)]
        public string ApiSecret { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(100)]
        public string UserName { get; set; }

        [StringLength(200)]
        public string? Password { get; set; }

        [StringLength(50)]
        public string PlatformKey { get; set; }

        [Required]
        [StringLength(100)]
        public string PlatformRestaurantId { get; set; }
        
        /// <summary>
        /// Optional link to FoodicsAccount
        /// Used to establish which Foodics account data should be synced to this Talabat vendor
        /// </summary>
        public Guid? FoodicsAccountId { get; set; }

        /// <summary>
        /// Specific Foodics branch ID to sync to this Talabat account.
        /// Required when SyncAllBranches is false.
        /// </summary>
        [StringLength(100)]
        public string? FoodicsBranchId { get; set; }

        /// <summary>
        /// Display name of the selected branch (for UI display)
        /// </summary>
        [StringLength(200)]
        public string? FoodicsBranchName { get; set; }

        /// <summary>
        /// Whether to sync all branches or only the specific branch.
        /// Default: true for backward compatibility
        /// </summary>
        public bool SyncAllBranches { get; set; } = true;

        /// <summary>
        /// Specific Foodics group ID to sync to this Talabat account.
        /// If specified, only products belonging to this group will be synced.
        /// Products not belonging to any group will be EXCLUDED.
        /// </summary>
        [StringLength(100)]
        public string? FoodicsGroupId { get; set; }

        /// <summary>
        /// Display name of the selected group (for UI display)
        /// </summary>
        [StringLength(200)]
        public string? FoodicsGroupName { get; set; }
    }
}

