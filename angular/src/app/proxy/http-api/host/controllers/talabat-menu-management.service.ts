import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { IActionResult } from '../../../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class TalabatMenuManagementService {
  apiName = 'Default';
  

  clearDefaultVendorMenuByCancellationToken = (cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/talabat/menu/clear',
    },
    { apiName: this.apiName,...config });
  

  clearVendorMenuByVendorCodeAndCancellationToken = (vendorCode: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/talabat/menu/clear/${vendorCode}`,
    },
    { apiName: this.apiName,...config });
  

  getBranchAvailabilityByVendorCodeAndCancellationToken = (vendorCode: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/talabat/menu/branch-availability/${vendorCode}`,
    },
    { apiName: this.apiName,...config });
  

  getMenuStatusByVendorCodeAndCancellationToken = (vendorCode: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/talabat/menu/status/${vendorCode}`,
    },
    { apiName: this.apiName,...config });
  

  hideAllItemsByVendorCodeAndCancellationToken = (vendorCode: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/talabat/menu/hide-all/${vendorCode}`,
    },
    { apiName: this.apiName,...config });
  

  setBranchAvailableByVendorCodeAndCancellationToken = (vendorCode: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/talabat/menu/branch-available/${vendorCode}`,
    },
    { apiName: this.apiName,...config });
  

  setBranchBusyByVendorCodeAndReasonAndAvailableInMinutesAndCancellationToken = (vendorCode: string, reason?: string, availableInMinutes?: number, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/talabat/menu/branch-busy/${vendorCode}`,
      params: { reason, availableInMinutes },
    },
    { apiName: this.apiName,...config });
  

  showAllItemsByVendorCodeAndCancellationToken = (vendorCode: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/talabat/menu/show-all/${vendorCode}`,
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
