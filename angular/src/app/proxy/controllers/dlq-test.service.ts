import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class DlqTestService {
  apiName = 'Default';
  

  getToken = (accountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/dlq-test/token/${accountId}`,
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
