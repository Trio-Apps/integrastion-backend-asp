import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { PagedResultDto } from '@abp/ng.core';
import type { GetTalabatOrderLogsInput, TalabatOrderLogDto } from './models';

@Injectable({
  providedIn: 'root',
})
export class TalabatOrderLogsService {
  apiName = 'Default';

  getList = (input: GetTalabatOrderLogsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TalabatOrderLogDto>>(
      {
        method: 'GET',
        url: '/api/app/talabat-order-log',
        params: { ...input },
      },
      { apiName: this.apiName, ...config }
    );

  retry = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'POST',
        url: `/api/app/talabat-order-log/retry/${id}`,
      },
      { apiName: this.apiName, ...config }
    );

  constructor(private restService: RestService) {}
}
