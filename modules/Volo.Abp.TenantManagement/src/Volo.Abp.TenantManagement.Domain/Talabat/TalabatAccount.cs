using System;
using Foodics;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Volo.Abp.TenantManagement.Talabat
{
    public class TalabatAccount : FullAuditedEntity<Guid>, IMultiTenant
    {
        public string Name { get; set; }
        public string VendorCode { get; set; }
        public string ChainCode { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
        public bool IsActive { get; set; } = true;
        public string? UserName { get; set; }
        public string? Password { get; set; }
        
        /// <summary>
        /// Platform key - e.g., "TB" for Talabat Brand, "TB_KW" for Kuwait
        /// </summary>
        public string? PlatformKey { get; set; }
        
        /// <summary>
        /// Platform's internal restaurant ID from Talabat
        /// </summary>
        public string PlatformRestaurantId { get; set; } = string.Empty;

        /// <summary>
        /// Link to FoodicsAccount for this vendor.
        /// Used to establish which Foodics account data should be synced to this Talabat account.
        /// This enables multi-tenant setup where each tenant can have multiple Foodics accounts
        /// syncing to multiple Talabat vendors.
        /// 
        /// Example:
        /// Tenant: "Pick Restaurant Chain"
        ///   ├── FoodicsAccount (Id: xxx-111, BrandName: "Pick Main")
        ///   └── TalabatAccount (VendorCode: "PH-SIDDIQ-001", FoodicsAccountId: xxx-111)
        /// 
        /// When syncing, system will:
        /// 1. Get products from FoodicsAccount (xxx-111)
        /// 2. Send them to Talabat vendor PH-SIDDIQ-001
        /// </summary>
        public Guid? FoodicsAccountId { get; set; }

        /// <summary>
        /// Navigation property to linked FoodicsAccount
        /// </summary>
        public virtual FoodicsAccount? FoodicsAccount { get; set; }

        /// <summary>
        /// Specific Foodics branch ID to sync to this Talabat account.
        /// If null, will sync all branches. If specified, only products available 
        /// in this branch will be synced to Talabat.
        /// 
        /// Example:
        /// - FoodicsBranchId = "branch-001" → Only sync products from branch-001
        /// - FoodicsBranchId = null → Sync all products from all branches
        /// </summary>
        public string? FoodicsBranchId { get; set; }

        /// <summary>
        /// Display name of the selected Foodics branch (denormalized for quick UI access)
        /// </summary>
        public string? FoodicsBranchName { get; set; }

        /// <summary>
        /// Whether to sync all branches or only the specific branch.
        /// - true: Sync all products regardless of branch (legacy behavior)
        /// - false: Only sync products from FoodicsBranchId (recommended)
        /// </summary>
        public bool SyncAllBranches { get; set; } = true; // Default to legacy behavior for backward compatibility

        /// <summary>
        /// Specific Foodics group ID to sync to this Talabat account.
        /// If null, will sync all products (no group filtering).
        /// If specified, only products belonging to this group will be synced to Talabat.
        /// Products not belonging to any group will be EXCLUDED when a group is configured.
        /// 
        /// Example:
        /// - FoodicsGroupId = "group-001" → Only sync products that belong to group-001
        /// - FoodicsGroupId = null → Sync all products (no group filtering)
        /// </summary>
        public string? FoodicsGroupId { get; set; }

        /// <summary>
        /// Display name of the selected Foodics group (denormalized for quick UI access)
        /// </summary>
        public string? FoodicsGroupName { get; set; }

        public Guid? TenantId { get; set; }
    }
}

