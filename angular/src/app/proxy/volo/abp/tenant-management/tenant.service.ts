import type { GetTenantsInput, TenantCreateDto, TenantDto, TenantUpdateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TenantService {
  apiName = 'AbpTenantManagement';
  

  create = (input: TenantCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantDto>({
      method: 'POST',
      url: '/api/multi-tenancy/tenants',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/multi-tenancy/tenants/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteDefaultConnectionString = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/multi-tenancy/tenants/${id}/default-connection-string`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantDto>({
      method: 'GET',
      url: `/api/multi-tenancy/tenants/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getDefaultConnectionString = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      responseType: 'text',
      url: `/api/multi-tenancy/tenants/${id}/default-connection-string`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetTenantsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TenantDto>>({
      method: 'GET',
      url: '/api/multi-tenancy/tenants',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: TenantUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantDto>({
      method: 'PATCH',
      url: `/api/multi-tenancy/tenants/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateDefaultConnectionString = (id: string, defaultConnectionString: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/multi-tenancy/tenants/${id}/default-connection-string`,
      params: { defaultConnectionString },
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
