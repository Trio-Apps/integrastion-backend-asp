import type { AcknowledgeRequest, UpdatePriorityRequest } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class DlqManagementService {
  apiName = 'Default';
  

  acknowledgeMessage = (id: string, request?: AcknowledgeRequest, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/dlq/messages/${id}/acknowledge`,
      body: request,
    },
    { apiName: this.apiName,...config });
  

  getDlqStats = (cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/dlq/stats',
    },
    { apiName: this.apiName,...config });
  

  getMessageById = (id: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/dlq/messages/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getPendingMessages = (eventType?: string, priority?: string, maxRecords: number = 100, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/dlq/messages',
      params: { eventType, priority, maxRecords },
    },
    { apiName: this.apiName,...config });
  

  replayMessage = (id: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/dlq/messages/${id}/replay`,
    },
    { apiName: this.apiName,...config });
  

  updatePriority = (id: string, request: UpdatePriorityRequest, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'PATCH',
      url: `/api/dlq/messages/${id}/priority`,
      body: request,
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
