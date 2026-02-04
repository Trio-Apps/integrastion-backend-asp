import type { GetSyncLogsInput, TalabatBranchStatusDto, TalabatDashboardDto, TalabatSyncLogItemDto, TalabatVendorLookupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TalabatDashboardService {
  apiName = 'Default';
  

  getVendors = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatVendorLookupDto[]>({
      method: 'GET',
      url: '/api/app/talabat-dashboard/vendors',
    },
    { apiName: this.apiName,...config });
  

  getBranchStatus = (vendorCode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatBranchStatusDto>({
      method: 'GET',
      url: '/api/app/talabat-dashboard/branch-status',
      params: { vendorCode },
    },
    { apiName: this.apiName,...config });
  

  getDashboard = (vendorCode?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatDashboardDto>({
      method: 'GET',
      url: '/api/app/talabat-dashboard/dashboard',
      params: { vendorCode },
    },
    { apiName: this.apiName,...config });
  

  getSyncLogs = (input: GetSyncLogsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TalabatSyncLogItemDto>>({
      method: 'GET',
      url: '/api/app/talabat-dashboard/sync-logs',
      params: { vendorCode: input.vendorCode, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  setBranchAvailable = (vendorCode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatBranchStatusDto>({
      method: 'POST',
      url: '/api/app/talabat-dashboard/set-branch-available',
      params: { vendorCode },
    },
    { apiName: this.apiName,...config });
  

  setBranchBusy = (vendorCode: string, reason?: string, availableInMinutes?: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatBranchStatusDto>({
      method: 'POST',
      url: '/api/app/talabat-dashboard/set-branch-busy',
      params: { vendorCode, reason, availableInMinutes },
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
