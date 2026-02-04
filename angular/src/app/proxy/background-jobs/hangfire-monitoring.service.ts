import type { HangfireDashboardDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class HangfireMonitoringService {
  apiName = 'Default';
  

  getDashboard = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, HangfireDashboardDto>({
      method: 'GET',
      url: '/api/app/hangfire-monitoring/dashboard',
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
