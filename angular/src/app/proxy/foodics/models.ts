import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateUpdateFoodicsAccountDto extends EntityDto {
  oAuthClientId?: string;
  oAuthClientSecret?: string;
  accessToken?: string;
  brandName?: string;
  apiEnvironment?: string;
}

export interface FoodicsAccountDto extends FullAuditedEntityDto<string> {
  oAuthClientId?: string;
  oAuthClientSecret?: string;
  brandName?: string;
  accessToken?: string;
  apiEnvironment?: string;
}

export interface FoodicsConnectionTestResultDto {
  success: boolean;
  message?: string;
  details?: string;
  apiEnvironment?: string;
  accessTokenConfigured: boolean;
  testedAtUtc?: string;
}
