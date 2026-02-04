import type { HealthCheckResult, KafkaTestResult } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { ActionResult } from '../../../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class HealthCheckService {
  apiName = 'Default';
  

  getStatus = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActionResult<HealthCheckResult>>({
      method: 'GET',
      url: '/api/health/status',
    },
    { apiName: this.apiName,...config });
  

  testKafkaConnectivity = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActionResult<KafkaTestResult>>({
      method: 'GET',
      url: '/api/health/kafka-test',
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
