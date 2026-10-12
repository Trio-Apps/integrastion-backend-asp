import { Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';

export interface SmartSearchOrderDto {
  id: string;
  orderCode?: string;
  shortCode?: string;
  orderToken?: string;
  vendorCode?: string;
  customerName?: string;
  customerPhone?: string;
  status?: string;
  receivedAt?: string;
}

export interface SmartSearchItemDto {
  id: string;
  foodicsProductId?: string;
  name: string;
  nameLocalized?: string;
  sku?: string;
  categoryName?: string;
  isActive: boolean;
  matchedModifiers: string[];
}

export interface SmartSearchBranchDto {
  branchId: string;
  branchName: string;
}

export interface SmartSearchResultDto {
  keyword: string;
  orders: SmartSearchOrderDto[];
  items: SmartSearchItemDto[];
  branches: SmartSearchBranchDto[];
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class SmartSearchService {
  apiName = 'Default';

  constructor(private restService: RestService) {}

  search = (keyword: string, maxResultsPerGroup = 8) =>
    this.restService.request<any, SmartSearchResultDto>(
      { method: 'POST', url: '/api/app/smart-search/search', body: { keyword, maxResultsPerGroup } },
      { apiName: this.apiName },
    );
}
