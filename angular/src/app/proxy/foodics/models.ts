import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateUpdateFoodicsAccountDto extends EntityDto {
  oAuthClientId?: string;
  oAuthClientSecret?: string;
  accessToken?: string;
  brandName?: string;
}

export interface FoodicsAccountDto extends FullAuditedEntityDto<string> {
  oAuthClientId?: string;
  oAuthClientSecret?: string;
  brandName?: string;
  accessToken?: string;
}
