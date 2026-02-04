
export interface MenuGroupMappingStatsDto {
  totalSyncs: number;
  successfulSyncs: number;
  failedSyncs: number;
  lastSuccessfulSync?: string;
  lastFailedSync?: string;
  averageSyncDuration?: string;
  totalItemsSynced: number;
  activeItemCount: number;
  categoryCount: number;
  successRate: number;
}

export interface MenuGroupSyncHistoryDto {
  id?: string;
  syncedAt?: string;
  isSuccess: boolean;
  errorMessage?: string;
  itemsSynced: number;
  itemsSkipped: number;
  itemsFailed: number;
  duration?: string;
  talabatImportId?: string;
  syncType?: string;
  initiatedBy?: string;
}

export interface MenuGroupSyncPreviewDto {
  totalItems: number;
  newItems: number;
  updatedItems: number;
  unchangedItems: number;
  categories: number;
  categoryNames: string[];
  sampleItems: MenuItemPreviewDto[];
  validationWarnings: string[];
  previewGeneratedAt?: string;
}

export interface MenuGroupSyncResult {
  isSuccess: boolean;
  errorMessage?: string;
  itemsSynced: number;
  itemsSkipped: number;
  itemsFailed: number;
  duration?: string;
  talabatImportId?: string;
  syncedAt?: string;
  warnings: string[];
}

export interface MenuItemPreviewDto {
  foodicsId?: string;
  name?: string;
  categoryName?: string;
  price?: number;
  syncAction?: string;
  changes: string[];
}

export interface TalabatConnectivityTestResult {
  isConnected: boolean;
  errorMessage?: string;
  responseTime?: string;
  talabatVersion?: string;
  testedAt?: string;
}

export interface TalabatVendorInfoDto {
  vendorCode?: string;
  vendorName?: string;
  chainCode?: string;
  isActive: boolean;
  existingMappingCount: number;
}
