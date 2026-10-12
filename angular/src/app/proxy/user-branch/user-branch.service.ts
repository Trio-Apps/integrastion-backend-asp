import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { UserBranchDto, UpdateUserBranchesDto } from './models';
import type { FoodicsBranchDto } from '../application/integrations/foodics/models';

@Injectable({ providedIn: 'root' })
export class UserBranchService {
  apiName = 'Default';

  getAvailableBranches = (foodicsAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsBranchDto[]>({
      method: 'GET',
      url: `/api/app/user-branch/available-branches/${foodicsAccountId}`,
    }, { apiName: this.apiName, ...config });

  getForUser = (userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserBranchDto[]>({
      method: 'GET',
      url: `/api/app/user-branch/for-user/${userId}`,
    }, { apiName: this.apiName, ...config });

  updateForUser = (userId: string, input: UpdateUserBranchesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/user-branch/for-user/${userId}`,
      body: input,
    }, { apiName: this.apiName, ...config });

  constructor(private restService: RestService) {}
}
