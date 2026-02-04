import type { DeleteItemsRequest } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { TalabatBranchItemAvailabilityRequest, TalabatMultiBranchAvailabilityRequest } from '../../../application/contracts/integrations/talabat/models';
import type { IActionResult } from '../../../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class TalabatTestService {
  apiName = 'Default';
  

  clearCatalog = (confirm?: boolean, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'DELETE',
      url: '/api/talabat/test/clear-catalog',
      params: { confirm },
    },
    { apiName: this.apiName,...config });
  

  clearVendorCatalog = (vendorCode: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/talabat/test/clear-vendor/${vendorCode}`,
    },
    { apiName: this.apiName,...config });
  

  deleteSpecificItems = (request: DeleteItemsRequest, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/talabat/test/delete-items',
      body: request,
    },
    { apiName: this.apiName,...config });
  

  getConfig = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/talabat/test/config',
    },
    { apiName: this.apiName,...config });
  

  getImportStatusByChainCode = (chainCode?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/talabat/test/import-status/{chainCode}`,
      params: { chainCode },
    },
    { apiName: this.apiName,...config });
  

  hideAllItems = (vendorCode?: string, confirm?: boolean, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/talabat/test/hide-all-items',
      params: { vendorCode, confirm },
    },
    { apiName: this.apiName,...config });
  

  previewCatalog = (vendorCode?: string, branchId?: string, limit: number = 10, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/talabat/test/preview',
      params: { vendorCode, branchId, limit },
    },
    { apiName: this.apiName,...config });
  

  showAllItems = (vendorCode?: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/talabat/test/show-all-items',
      params: { vendorCode },
    },
    { apiName: this.apiName,...config });
  

  testLogin = (cancellationToken: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/talabat/test/login',
    },
    { apiName: this.apiName,...config });
  

  testSync = (chainCode?: string, branchId?: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/talabat/test/sync',
      params: { chainCode, branchId },
    },
    { apiName: this.apiName,...config });
  

  testSyncV2 = (chainCode?: string, branchId?: string, submit?: boolean, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/talabat/test/sync-v2',
      params: { chainCode, branchId, submit },
    },
    { apiName: this.apiName,...config });
  

  toggleItemAvailabilityByVendorCodeAndRemoteCodeAndAvailableAndReason = (vendorCode: string, remoteCode: string, available: boolean = true, reason?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/talabat/test/branch/${vendorCode}/item/${remoteCode}/toggle`,
      params: { available, reason },
    },
    { apiName: this.apiName,...config });
  

  triggerFull = (foodicsAccountId?: string, branchId?: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/talabat/test/trigger-full',
      params: { foodicsAccountId, branchId },
    },
    { apiName: this.apiName,...config });
  

  updateBranchItemAvailabilityByVendorCodeAndRequest = (vendorCode: string, request: TalabatBranchItemAvailabilityRequest, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/talabat/test/branch/${vendorCode}/availability`,
      body: request,
    },
    { apiName: this.apiName,...config });
  

  updateMultiBranchAvailabilityByRequest = (request: TalabatMultiBranchAvailabilityRequest, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/talabat/test/branches/availability',
      body: request,
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
