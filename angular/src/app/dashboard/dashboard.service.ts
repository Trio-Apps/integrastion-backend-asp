import { Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';

export interface DashboardOrderStatsDto {
  total: number;
  today: number;
  last7Days: number;
  succeeded: number;
  failed: number;
  processing: number;
  enqueued: number;
  successRate: number;
  revenue7Days: number;
}

export interface DashboardDailyCountDto {
  date: string;
  count: number;
}

export interface DashboardSyncStatsDto {
  productsSynced: number;
  submissionsTotal: number;
  submissionsSuccessful: number;
  submissionsFailed: number;
  lastSyncAt?: string;
  lastSyncStatus?: string;
}

export interface DashboardSetupStatsDto {
  foodicsAccounts: number;
  talabatVendors: number;
  activeBranches: number;
}

export interface DashboardRecentOrderDto {
  id: string;
  orderCode?: string;
  customerName?: string;
  vendorCode: string;
  grandTotal?: number;
  status: string;
  receivedAt: string;
}

export interface DashboardOrderCountDto {
  date: string;
  total: number;
  succeeded: number;
  failed: number;
  inProgress: number;
  revenue: number;
}

export interface DashboardBranchDto {
  vendorCode: string;
  name?: string;
  foodicsBranchName?: string;
}

export interface DashboardOverviewDto {
  orders: DashboardOrderStatsDto;
  ordersTrend: DashboardDailyCountDto[];
  sync: DashboardSyncStatsDto;
  setup: DashboardSetupStatsDto;
  recentOrders: DashboardRecentOrderDto[];
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  apiName = 'Default';

  constructor(private restService: RestService) {}

  /** Omit vendorCode for all branches the user can see. */
  getOverview = (vendorCode?: string) =>
    this.restService.request<any, DashboardOverviewDto>(
      { method: 'GET', url: '/api/app/dashboard/overview', params: vendorCode ? { vendorCode } : {} },
      { apiName: this.apiName },
    );

  /** Orders received on one calendar day. Omit the date for today, vendorCode for all branches. */
  getOrderCount = (date?: string, vendorCode?: string) =>
    this.restService.request<any, DashboardOrderCountDto>(
      {
        method: 'GET',
        url: '/api/app/dashboard/order-count',
        params: { ...(date ? { date } : {}), ...(vendorCode ? { vendorCode } : {}) },
      },
      { apiName: this.apiName },
    );

  /** Talabat branches the current user may filter the dashboard by. */
  getBranches = () =>
    this.restService.request<any, DashboardBranchDto[]>(
      { method: 'GET', url: '/api/app/dashboard/branches' },
      { apiName: this.apiName },
    );
}
