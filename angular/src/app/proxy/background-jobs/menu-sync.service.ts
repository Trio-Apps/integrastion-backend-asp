import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { FoodicsAggregatedMenuDto, FoodicsBranchDto, FoodicsEnhancedAggregatedMenuDto, FoodicsMenuDisplayResponseDto, GetEnhancedAggregatedMenuRequest } from '../application/integrations/foodics/models';
import type { FoodicsGroupWithProductCountDto, GetStagingMenuGroupSummaryRequest, StagingMenuGroupSummaryDto } from './models';

@Injectable({
  providedIn: 'root',
})
export class MenuSyncService {
  apiName = 'Default';
  

  get = (branchId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsMenuDisplayResponseDto>({
      method: 'GET',
      url: '/api/app/menu-sync',
      params: { branchId },
    },
    { apiName: this.apiName,...config });
  

  getAggregated = (branchId?: string, foodicsAccountId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsAggregatedMenuDto>({
      method: 'GET',
      url: '/api/app/menu-sync/aggregated',
      params: { branchId, foodicsAccountId },
    },
    { apiName: this.apiName,...config });
  

  getEnhancedAggregated = (request: GetEnhancedAggregatedMenuRequest, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsEnhancedAggregatedMenuDto>({
      method: 'GET',
      url: '/api/app/menu-sync/enhanced-aggregated',
      params: { branchId: request.branchId, foodicsAccountId: request.foodicsAccountId, includeProductDetails: request.includeProductDetails, includeInactiveProducts: request.includeInactiveProducts, includeUncategorizedProducts: request.includeUncategorizedProducts },
    },
    { apiName: this.apiName,...config });
  

  triggerMenuSync = (foodicsAccountId?: string, branchId?: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/menu-sync/trigger-menu-sync',
      params: { foodicsAccountId, branchId },
    },
    { apiName: this.apiName,...config });

  getBranchesForAccount = (foodicsAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsBranchDto[]>({
      method: 'GET',
      url: '/api/app/menu-sync/branches-for-account',
      params: { foodicsAccountId },
    },
    { apiName: this.apiName,...config });

  getGroupsForAccount = (foodicsAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsGroupWithProductCountDto[]>({
      method: 'GET',
      url: '/api/menu-sync/groups-for-account',
      params: { foodicsAccountId },
    },
    { apiName: this.apiName,...config });

  getStagingMenuGroupSummary = (request: GetStagingMenuGroupSummaryRequest, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StagingMenuGroupSummaryDto[]>({
      method: 'GET',
      url: '/api/app/menu-sync/staging-menu-group-summary',
      params: { foodicsAccountId: request.foodicsAccountId, branchId: request.branchId },
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
