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

export interface FoodicsAuthorizationUrlDto {
  authorizationUrl?: string;
  state?: string;
  redirectUri?: string;
}

export interface CompleteFoodicsAuthorizationDto {
  code?: string;
  state?: string;
}

export interface FoodicsOAuthCallbackResultDto {
  success: boolean;
  foodicsAccountId?: string;
  message?: string;
  details?: string;
}
