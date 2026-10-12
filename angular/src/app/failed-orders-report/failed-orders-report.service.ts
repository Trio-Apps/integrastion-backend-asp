import { Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';

export interface FailedOrdersReportSettingsDto {
  email?: string;
  time: string;
  timeZone: string;
}

export interface UpdateFailedOrdersReportSettingsInput {
  email?: string;
  time: string;
}

export interface FailedOrdersReportTestResultDto {
  sent: boolean;
  failedOrderCount: number;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class FailedOrdersReportService {
  apiName = 'Default';

  constructor(private restService: RestService) {}

  get = () =>
    this.restService.request<any, FailedOrdersReportSettingsDto>(
      { method: 'GET', url: '/api/app/failed-orders-report' },
      { apiName: this.apiName },
    );

  update = (input: UpdateFailedOrdersReportSettingsInput) =>
    this.restService.request<any, FailedOrdersReportSettingsDto>(
      { method: 'PUT', url: '/api/app/failed-orders-report', body: input },
      { apiName: this.apiName },
    );

  sendTest = () =>
    this.restService.request<any, FailedOrdersReportTestResultDto>(
      { method: 'POST', url: '/api/app/failed-orders-report/send-test' },
      { apiName: this.apiName },
    );
}
