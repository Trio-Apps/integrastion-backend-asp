import type { CreateUpdateFoodicsAccountDto, FoodicsAccountDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FoodicsService {
  apiName = 'AbpTenantManagement';
  

  create = (input: CreateUpdateFoodicsAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsAccountDto>({
      method: 'POST',
      url: '/api/foodics',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/foodics',
      params: { id },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<FoodicsAccountDto>>({
      method: 'GET',
      url: '/api/foodics',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateFoodicsAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsAccountDto>({
      method: 'PATCH',
      url: `/api/foodics/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
