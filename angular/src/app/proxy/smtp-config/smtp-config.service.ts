import type { CreateUpdateSmtpConfigDto, SmtpConfigDto, SmtpTestResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SmtpConfigService {
  apiName = 'AbpTenantManagement';

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmtpConfigDto | null>({
      method: 'GET',
      url: '/api/smtp-config',
    }, { apiName: this.apiName, ...config });

  save = (input: CreateUpdateSmtpConfigDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmtpConfigDto>({
      method: 'PUT',
      url: '/api/smtp-config',
      body: input,
    }, { apiName: this.apiName, ...config });

  test = (input: CreateUpdateSmtpConfigDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmtpTestResultDto>({
      method: 'POST',
      url: '/api/smtp-config/test',
      body: input,
    }, { apiName: this.apiName, ...config });

  constructor(private restService: RestService) {}
}
