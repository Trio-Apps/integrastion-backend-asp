import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class TalabatWebhookService {
  apiName = 'Default';
  

  catalogImportStatus = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/talabat/webhooks/catalog-status',
    },
    { apiName: this.apiName,...config });
  

  health = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/talabat/webhooks/health',
    },
    { apiName: this.apiName,...config });
  

  menuImportRequest = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/talabat/webhooks/menu-import-request',
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
