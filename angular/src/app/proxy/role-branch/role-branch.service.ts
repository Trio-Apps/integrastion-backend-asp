import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { RoleBranchDto, UpdateRoleBranchesDto } from './models';
import type { FoodicsBranchDto } from '../application/integrations/foodics/models';

@Injectable({ providedIn: 'root' })
export class RoleBranchService {
  apiName = 'Default';

  getAvailableBranches = (foodicsAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsBranchDto[]>({
      method: 'GET',
      url: `/api/app/role-branch/available-branches/${foodicsAccountId}`,
    }, { apiName: this.apiName, ...config });

  getForRole = (roleId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RoleBranchDto[]>({
      method: 'GET',
      url: `/api/app/role-branch/for-role/${roleId}`,
    }, { apiName: this.apiName, ...config });

  updateForRole = (roleId: string, input: UpdateRoleBranchesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/role-branch/for-role/${roleId}`,
      body: input,
    }, { apiName: this.apiName, ...config });

  constructor(private restService: RestService) {}
}
