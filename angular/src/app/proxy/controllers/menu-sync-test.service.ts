import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class MenuSyncTestService {
  apiName = 'Default';
  

  executeDirect = (foodicsAccountId?: string, branchId?: string, skipIdempotency: boolean = true, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/test/menu-sync/execute-direct',
      params: { foodicsAccountId, branchId, skipIdempotency },
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
