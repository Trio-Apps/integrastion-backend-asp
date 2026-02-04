import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class TalabatSubmissionTestService {
  apiName = 'Default';
  

  getStagingStats = (cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/test/talabat-submission/staging-stats',
    },
    { apiName: this.apiName,...config });
  

  getTalabatConfig = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/test/talabat-submission/talabat-config',
    },
    { apiName: this.apiName,...config });
  

  submitFromStaging = (foodicsAccountId?: string, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: '/api/test/talabat-submission/submit-from-staging',
      params: { foodicsAccountId },
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
