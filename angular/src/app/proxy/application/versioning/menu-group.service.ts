import type { AssignCategoryDto, CategorySortOrderDto, CreateMenuGroupDto, MenuGroupCategoryDto, MenuGroupDto, MenuGroupStatisticsDto, MenuGroupValidationResultDto, UpdateMenuGroupDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MenuGroupService {
  apiName = 'Default';
  

  activate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/menu-group/${id}/activate`,
    },
    { apiName: this.apiName,...config });
  

  assignCategory = (menuGroupId: string, input: AssignCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupCategoryDto>({
      method: 'POST',
      url: `/api/app/menu-group/assign-category/${menuGroupId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateMenuGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupDto>({
      method: 'POST',
      url: '/api/app/menu-group',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deactivate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/menu-group/${id}/deactivate`,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/menu-group/${id}`,
    },
    { apiName: this.apiName,...config });
  

  duplicate = (id: string, newName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupDto>({
      method: 'POST',
      url: `/api/app/menu-group/${id}/duplicate`,
      params: { newName },
    },
    { apiName: this.apiName,...config });
  

  findByCategoryByFoodicsAccountIdAndCategoryId = (foodicsAccountId: string, categoryId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupDto[]>({
      method: 'POST',
      url: '/api/app/menu-group/find-by-category',
      params: { foodicsAccountId, categoryId },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupDto>({
      method: 'GET',
      url: `/api/app/menu-group/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getActiveByAccountAndBranch = (foodicsAccountId: string, branchId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupDto[]>({
      method: 'GET',
      url: '/api/app/menu-group/active-by-account-and-branch',
      params: { foodicsAccountId, branchId },
    },
    { apiName: this.apiName,...config });
  

  getByAccountAndBranch = (foodicsAccountId: string, branchId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupDto[]>({
      method: 'GET',
      url: '/api/app/menu-group/by-account-and-branch',
      params: { foodicsAccountId, branchId },
    },
    { apiName: this.apiName,...config });
  

  getCategories = (menuGroupId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupCategoryDto[]>({
      method: 'GET',
      url: `/api/app/menu-group/categories/${menuGroupId}`,
    },
    { apiName: this.apiName,...config });
  

  getStatistics = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupStatisticsDto>({
      method: 'GET',
      url: `/api/app/menu-group/${id}/statistics`,
    },
    { apiName: this.apiName,...config });
  

  removeCategory = (menuGroupId: string, categoryId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/menu-group/category',
      params: { menuGroupId, categoryId },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateMenuGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupDto>({
      method: 'PUT',
      url: `/api/app/menu-group/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateCategorySortOrder = (menuGroupId: string, sortOrders: CategorySortOrderDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/menu-group/category-sort-order/${menuGroupId}`,
      body: sortOrders,
    },
    { apiName: this.apiName,...config });
  

  validateForSync = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuGroupValidationResultDto>({
      method: 'POST',
      url: `/api/app/menu-group/${id}/validate-for-sync`,
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
