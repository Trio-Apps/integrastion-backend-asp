import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class IdempotencyTestService {
  apiName = 'Default';
  

  clearAll = (cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'DELETE',
      url: '/api/test/idempotency/clear-all',
    },
    { apiName: this.apiName,...config });
  

  clearByAccount = (accountId: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'DELETE',
      url: `/api/test/idempotency/clear/${accountId}`,
    },
    { apiName: this.apiName,...config });
  

  list = (cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/test/idempotency/list',
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
