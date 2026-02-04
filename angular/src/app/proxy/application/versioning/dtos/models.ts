import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface AssignCategoryDto {
  categoryId?: string;
  sortOrder: number;
}

export interface CategorySortOrderDto {
  categoryId?: string;
  sortOrder: number;
}

export interface CategoryStatisticsDto {
  categoryId?: string;
  categoryName?: string;
  productsCount: number;
  activeProductsCount: number;
  lastUpdated?: string;
}

export interface CreateMenuGroupDto {
  foodicsAccountId?: string;
  branchId?: string;
  name?: string;
  description?: string;
  sortOrder: number;
  metadataJson?: string;
  categoryIds: string[];
}

export interface CreateMenuGroupTalabatMappingDto {
  menuGroupId: string;
  talabatVendorCode: string;
  talabatMenuId?: string;
  talabatMenuName?: string;
  talabatMenuDescription?: string;
  priority: number;
  mappingStrategy: string;
  configuration: MenuGroupMappingConfigurationDto;
}

export interface MenuGroupCategoryDto extends FullAuditedEntityDto<string> {
  menuGroupId?: string;
  categoryId?: string;
  isActive: boolean;
  sortOrder: number;
  assignedAt?: string;
  categoryName?: string;
  productsCount: number;
}

export interface MenuGroupDto extends FullAuditedEntityDto<string> {
  foodicsAccountId?: string;
  branchId?: string;
  name?: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
  metadataJson?: string;
  activeCategoriesCount: number;
  totalProductsCount: number;
  lastSyncedAt?: string;
  lastSyncStatus?: string;
  categories: MenuGroupCategoryDto[];
}

export interface MenuGroupMappingConfigurationDto {
  isolateMenu: boolean;
  namingPattern?: string;
  includeMenuGroupInNames: boolean;
  syncAvailability: boolean;
  syncPricing: boolean;
  syncModifiers: boolean;
  syncImages: boolean;
  customFieldMappings: Record<string, string>;
  validationRules: MenuGroupValidationRulesDto;
  syncPreferences: MenuGroupSyncPreferencesDto;
}

export interface MenuGroupStatisticsDto {
  menuGroupId?: string;
  totalCategories: number;
  activeCategories: number;
  totalProducts: number;
  activeProducts: number;
  successfulSyncs: number;
  failedSyncs: number;
  lastSyncDate?: string;
  averageSyncDuration?: string;
  categoryStatistics: CategoryStatisticsDto[];
}

export interface MenuGroupSyncPreferencesDto {
  autoSync: boolean;
  syncFrequencyMinutes?: number;
  offPeakOnly: boolean;
  timeZone?: string;
  preferredSyncHours: number[];
  batchSync: boolean;
  maxRetryAttempts: number;
  retryDelayMinutes: number;
}

export interface MenuGroupTalabatMappingDto extends FullAuditedEntityDto<string> {
  foodicsAccountId?: string;
  menuGroupId?: string;
  menuGroupName?: string;
  talabatVendorCode: string;
  talabatMenuId: string;
  talabatMenuName: string;
  talabatMenuDescription?: string;
  isActive: boolean;
  priority: number;
  mappingStrategy: string;
  configuration: MenuGroupMappingConfigurationDto;
  mappingEstablishedAt?: string;
  lastVerifiedAt?: string;
  syncCount: number;
  isTalabatValidated: boolean;
  talabatInternalMenuId?: string;
  syncStatus?: string;
  lastSyncError?: string;
}

export interface MenuGroupValidationResultDto {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  details: Record<string, object>;
}

export interface MenuGroupValidationRulesDto {
  minItemCount?: number;
  maxItemCount?: number;
  requiredCategories: string[];
  excludedCategories: string[];
  requirePrices: boolean;
  requireDescriptions: boolean;
  requireImages: boolean;
}

export interface UpdateMenuGroupDto {
  name?: string;
  description?: string;
  sortOrder: number;
  metadataJson?: string;
}

export interface UpdateMenuGroupTalabatMappingDto {
  talabatMenuName: string;
  talabatMenuDescription?: string;
  isActive: boolean;
  priority: number;
  mappingStrategy: string;
  configuration: MenuGroupMappingConfigurationDto;
}
