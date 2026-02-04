import type { MenuSyncReplayInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class MenuSyncService {
  apiName = 'Default';
  

  getStatus = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/menu-sync/status',
    },
    { apiName: this.apiName,...config });
  

  replayFromDlq = (input: MenuSyncReplayInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/menu-sync/replay',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  triggerMenuSync = (foodicsAccountId?: string, branchId?: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/menu-sync/trigger',
      params: { foodicsAccountId, branchId },
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
