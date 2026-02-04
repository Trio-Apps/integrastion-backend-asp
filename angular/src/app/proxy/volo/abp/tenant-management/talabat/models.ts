import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateUpdateTalabatAccountDto extends EntityDto {
  name: string;
  vendorCode: string;
  chainCode?: string;
  apiKey?: string;
  apiSecret?: string;
  isActive: boolean;
  userName?: string;
  platformKey?: string;
  platformRestaurantId?: string;
  foodicsAccountId?: string;
  foodicsBranchId?: string;
  foodicsBranchName?: string;
  syncAllBranches: boolean;
  foodicsGroupId?: string;
  foodicsGroupName?: string;
}

export interface TalabatAccountDto extends FullAuditedEntityDto<string> {
  name?: string;
  vendorCode?: string;
  chainCode?: string;
  apiKey?: string;
  apiSecret?: string;
  isActive: boolean;
  userName?: string;
  platformKey?: string;
  platformRestaurantId?: string;
  foodicsAccountId?: string;
  foodicsAccountName?: string;
  foodicsBranchId?: string;
  foodicsBranchName?: string;
  syncAllBranches: boolean;
  foodicsGroupId?: string;
  foodicsGroupName?: string;
}
