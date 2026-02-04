import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';
import type { ProductAvailabilitySyncResultDto } from '../application/integrations/foodics/models';

@Injectable({
  providedIn: 'root',
})
export class ProductAvailabilityService {
  apiName = 'Default';
  

  fetchAndPrepare = (page: number = 1, perPage: number = 100, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductAvailabilitySyncResultDto>({
      method: 'POST',
      url: '/api/app/product-availability/fetch-and-prepare',
      params: { page, perPage },
    },
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
