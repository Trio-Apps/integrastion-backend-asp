import type { CreateUpdateTalabatAccountDto, TalabatAccountDto } from './talabat/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TalabatAccountService {
  apiName = 'AbpTenantManagement';
  

  create = (input: CreateUpdateTalabatAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatAccountDto>({
      method: 'POST',
      url: '/api/app/talabat-account',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/talabat-account/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatAccountDto>({
      method: 'GET',
      url: `/api/app/talabat-account/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TalabatAccountDto>>({
      method: 'GET',
      url: '/api/app/talabat-account',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateTalabatAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatAccountDto>({
      method: 'PUT',
      url: `/api/app/talabat-account/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
