import type { CreateMenuGroupTalabatMappingDto, MenuGroupTalabatMappingDto, UpdateMenuGroupTalabatMappingDto } from './dtos/models';
import type { MenuGroupMappingStatsDto, MenuGroupSyncHistoryDto, MenuGroupSyncPreviewDto, MenuGroupSyncResult, TalabatConnectivityTestResult, TalabatVendorInfoDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { MenuMappingValidationResult } from '../../domain/versioning/models';

@Injectable({
  providedIn: 'root',
})
export class MenuGroupTalabatMappingService {
  apiName = 'Default';
  

  activateMapping = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/menu-group-talabat-mapping/${id}/activate-mapping`,
    },
    { apiName: this.apiName,...config });
  

  bulkCreateMappings = (inputs: CreateMenuGroupTalabatMappingDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupTalabatMappingDto[]>({
      method: 'POST',
      url: '/api/app/menu-group-talabat-mapping/bulk-create-mappings',
      body: inputs,
    },
    { apiName: this.apiName,...config });
  

  cloneMapping = (sourceMappingId: string, targetMenuGroupId: string, newTalabatMenuId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupTalabatMappingDto>({
      method: 'POST',
      url: '/api/app/menu-group-talabat-mapping/clone-mapping',
      params: { sourceMappingId, targetMenuGroupId, newTalabatMenuId },
    },
    { apiName: this.apiName,...config });
  

  createMapping = (input: CreateMenuGroupTalabatMappingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupTalabatMappingDto>({
      method: 'POST',
      url: '/api/app/menu-group-talabat-mapping/mapping',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deactivateMapping = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/menu-group-talabat-mapping/${id}/deactivate-mapping`,
    },
    { apiName: this.apiName,...config });
  

  deleteMapping = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/menu-group-talabat-mapping/${id}/mapping`,
    },
    { apiName: this.apiName,...config });
  

  exportMappingConfiguration = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      responseType: 'text',
      url: `/api/app/menu-group-talabat-mapping/${id}/export-mapping-configuration`,
    },
    { apiName: this.apiName,...config });
  

  generateSuggestedTalabatMenuId = (menuGroupId: string, talabatVendorCode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      responseType: 'text',
      url: `/api/app/menu-group-talabat-mapping/generate-suggested-talabat-menu-id/${menuGroupId}`,
      params: { talabatVendorCode },
    },
    { apiName: this.apiName,...config });
  

  getAvailableTalabatVendors = (foodicsAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatVendorInfoDto[]>({
      method: 'GET',
      url: `/api/app/menu-group-talabat-mapping/available-talabat-vendors/${foodicsAccountId}`,
    },
    { apiName: this.apiName,...config });
  

  getMapping = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupTalabatMappingDto>({
      method: 'GET',
      url: `/api/app/menu-group-talabat-mapping/${id}/mapping`,
    },
    { apiName: this.apiName,...config });
  

  getMappingByMenuGroup = (menuGroupId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupTalabatMappingDto>({
      method: 'GET',
      url: `/api/app/menu-group-talabat-mapping/mapping-by-menu-group/${menuGroupId}`,
    },
    { apiName: this.apiName,...config });
  

  getMappingStats = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupMappingStatsDto>({
      method: 'GET',
      url: `/api/app/menu-group-talabat-mapping/${id}/mapping-stats`,
    },
    { apiName: this.apiName,...config });
  

  getMappings = (foodicsAccountId: string, input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MenuGroupTalabatMappingDto>>({
      method: 'GET',
      url: `/api/app/menu-group-talabat-mapping/mappings/${foodicsAccountId}`,
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMappingsByVendor = (foodicsAccountId: string, talabatVendorCode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupTalabatMappingDto[]>({
      method: 'GET',
      url: `/api/app/menu-group-talabat-mapping/mappings-by-vendor/${foodicsAccountId}`,
      params: { talabatVendorCode },
    },
    { apiName: this.apiName,...config });
  

  getSyncHistory = (mappingId: string, input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MenuGroupSyncHistoryDto>>({
      method: 'GET',
      url: `/api/app/menu-group-talabat-mapping/sync-history/${mappingId}`,
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  importMappingConfiguration = (menuGroupId: string, configurationJson: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupTalabatMappingDto>({
      method: 'POST',
      url: `/api/app/menu-group-talabat-mapping/import-mapping-configuration/${menuGroupId}`,
      params: { configurationJson },
    },
    { apiName: this.apiName,...config });
  

  previewSync = (mappingId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupSyncPreviewDto>({
      method: 'POST',
      url: `/api/app/menu-group-talabat-mapping/preview-sync/${mappingId}`,
    },
    { apiName: this.apiName,...config });
  

  syncMenuGroup = (mappingId: string, forceFull?: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupSyncResult>({
      method: 'POST',
      url: `/api/app/menu-group-talabat-mapping/sync-menu-group/${mappingId}`,
      params: { forceFull },
    },
    { apiName: this.apiName,...config });
  

  testTalabatConnectivity = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatConnectivityTestResult>({
      method: 'POST',
      url: `/api/app/menu-group-talabat-mapping/${id}/test-talabat-connectivity`,
    },
    { apiName: this.apiName,...config });
  

  updateMapping = (id: string, input: UpdateMenuGroupTalabatMappingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupTalabatMappingDto>({
      method: 'PUT',
      url: `/api/app/menu-group-talabat-mapping/${id}/mapping`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  validateMapping = (input: CreateMenuGroupTalabatMappingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuMappingValidationResult>({
      method: 'POST',
      url: '/api/app/menu-group-talabat-mapping/validate-mapping',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  validateMapping = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuMappingValidationResult>({
      method: 'POST',
      url: `/api/app/menu-group-talabat-mapping/${id}/validate-mapping`,
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
