import { Injectable } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface GetMenuSyncRunsInput extends PagedAndSortedResultRequestDto {
  foodicsAccountId?: string;
  searchTerm?: string;
  status?: string;
  fromDate?: string;
  toDate?: string;
}

export interface MenuSyncRunSummaryDto {
  id: string;
  foodicsAccountId: string;
  branchId?: string;
  correlationId: string;
  syncType: string;
  triggerSource: string;
  status: string;
  result?: string;
  currentPhase?: string;
  progressPercentage: number;
  startedAt: string;
  completedAt?: string;
  durationSeconds?: number;
  totalProductsProcessed: number;
  productsSucceeded: number;
  productsFailed: number;
  productsSkipped: number;
  categoriesProcessed: number;
  modifiersProcessed: number;
  vendorSubmissionCount: number;
  failedVendorCount: number;
  missingVendorLogCount: number;
}

export interface MenuSyncRunDetailsDto extends MenuSyncRunSummaryDto {
  talabatVendorCode?: string;
  talabatImportId?: string;
  talabatSyncStatus?: string;
  talabatSubmittedAt?: string;
  talabatCompletedAt?: string;
  errorsJson?: string;
  warningsJson?: string;
  metricsJson?: string;
  configurationJson?: string;
  steps: MenuSyncRunStepDto[];
  vendors: MenuSyncVendorSubmissionDto[];
}

export interface MenuSyncRunStepDto {
  id: string;
  stepType: string;
  message: string;
  phase?: string;
  timestamp: string;
  sequenceNumber: number;
  durationSeconds?: number;
  dataJson?: string;
}

export interface MenuSyncVendorSubmissionDto {
  vendorCode: string;
  branchId?: string;
  branchName?: string;
  groupId?: string;
  groupName?: string;
  syncAllBranches: boolean;
  isActive: boolean;
  importId?: string;
  status: string;
  submittedAt?: string;
  completedAt?: string;
  productsCount: number;
  categoriesCount: number;
  productsCreated: number;
  productsUpdated: number;
  categoriesCreated: number;
  categoriesUpdated: number;
  errorsCount: number;
  responseMessage?: string;
  errorsJson?: string;
  payloadAvailable: boolean;
  payloadProducts: number;
  payloadToppings: number;
  payloadOptionProducts: number;
  payloadCategories: number;
  stagedProducts: number;
  stagedProductsWithModifiers: number;
  stagedModifierGroups: number;
  stagedRequiredModifierGroups: number;
  stagedModifierOptions: number;
  latestStagingSyncDate?: string;
  diagnostic?: string;
}

export interface MenuSyncVendorItemDto {
  foodicsProductId: string;
  name: string;
  nameLocalized?: string;
  categoryName?: string;
  price?: number;
  isActive: boolean;
  syncDate: string;
  talabatSyncStatus?: string;
  talabatImportId?: string;
  talabatSubmittedAt?: string;
  modifierGroupsCount: number;
  requiredModifierGroupsCount: number;
  modifierOptionsCount: number;
  modifiers: MenuSyncItemModifierDto[];
}

export interface MenuSyncItemModifierDto {
  id: string;
  name?: string;
  nameLocalized?: string;
  minimum: number;
  maximum: number;
  isRequired: boolean;
  optionsCount: number;
  options: MenuSyncItemModifierOptionDto[];
}

export interface MenuSyncItemModifierOptionDto {
  id: string;
  name?: string;
  nameLocalized?: string;
  price?: number;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class MenuSyncDiagnosticsService {
  apiName = 'Default';

  getRuns = (input: GetMenuSyncRunsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MenuSyncRunSummaryDto>>(
      {
        method: 'GET',
        url: '/api/menu-sync/runs',
        params: { ...input },
      },
      { apiName: this.apiName, ...config }
    );

  getRunDetails = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuSyncRunDetailsDto>(
      {
        method: 'GET',
        url: `/api/menu-sync/runs/${id}`,
      },
      { apiName: this.apiName, ...config }
    );

  getVendorItems = (id: string, vendorCode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuSyncVendorItemDto[]>(
      {
        method: 'GET',
        url: `/api/menu-sync/runs/${id}/vendors/${encodeURIComponent(vendorCode)}/items`,
      },
      { apiName: this.apiName, ...config }
    );

  constructor(private restService: RestService) {}
}
