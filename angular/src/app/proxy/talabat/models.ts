import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface GetSyncLogsInput extends PagedAndSortedResultRequestDto {
  vendorCode?: string;
  status?: string;
  fromDate?: string;
  toDate?: string;
}

export interface TalabatVendorLookupDto {
  vendorCode: string;
  name: string;
  platformRestaurantId?: string;
}

export interface TalabatBranchStatusDto {
  vendorCode?: string;
  isAvailable: boolean;
  status?: string;
  reason?: string;
  availableAt?: string;
  lastUpdated?: string;
}

export interface TalabatDashboardDto {
  counts: TalabatSyncCountsDto;
  recentSubmissions: TalabatSyncLogItemDto[];
  branchStatus: TalabatBranchStatusDto;
  stagingStats: TalabatStagingStatsDto;
}

export interface TalabatStagingStatsDto {
  totalProducts: number;
  activeProducts: number;
  inactiveProducts: number;
  submittedProducts: number;
  notSubmittedProducts: number;
  completedProducts: number;
  failedProducts: number;
  lastSyncDate?: string;
  lastSubmittedAt?: string;
  lastSyncStatus?: string;
}

export interface TalabatSyncCountsDto {
  totalSubmissions: number;
  successfulSubmissions: number;
  failedSubmissions: number;
  pendingSubmissions: number;
  totalProducts: number;
  activeProducts: number;
  syncedProducts: number;
}

export interface TalabatSyncLogItemDto {
  id?: string;
  vendorCode?: string;
  chainCode?: string;
  importId?: string;
  status?: string;
  submittedAt?: string;
  completedAt?: string;
  categoriesCount: number;
  productsCount: number;
  productsCreated?: number;
  productsUpdated?: number;
  errorsCount?: number;
  apiVersion?: string;
  processingDurationSeconds?: number;
  tenantId?: string;
  tenantName?: string;
}

export interface GetTalabatOrderLogsInput extends PagedAndSortedResultRequestDto {
  vendorCode?: string;
  status?: string;
  isTestOrder?: boolean;
  fromDate?: string;
  toDate?: string;
}

export interface TalabatOrderLogDto {
  id?: string;
  foodicsAccountId?: string;
  vendorCode?: string;
  platformRestaurantId?: string;
  orderToken?: string;
  orderCode?: string;
  shortCode?: string;
  status?: string;
  isTestOrder?: boolean;
  productsCount?: number;
  categoriesCount?: number;
  orderCreatedAt?: string;
  receivedAt?: string;
  lastAttemptAt?: string;
  attempts?: number;
  lastError?: string;
  creationTime?: string;
}
