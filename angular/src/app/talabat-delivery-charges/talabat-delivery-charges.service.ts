import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';

export interface TalabatDeliveryChargeDto {
  id: string;
  name: string;
  nameLocalized?: string;
  type: number;
  isOpenCharge: boolean;
  isAutoApplied: boolean;
  isCalculatedUsingSubtotal: boolean;
  value?: number;
  orderTypes: number[];
}

export interface TalabatDeliveryChargeSettingsDto {
  activeDeliveryChargeId?: string;
  activeDeliveryChargeName?: string;
  source?: string;
  charges: TalabatDeliveryChargeDto[];
}

export interface UpdateTalabatActiveDeliveryChargeInput {
  deliveryChargeId?: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class TalabatDeliveryChargesService {
  apiName = 'Default';

  constructor(private readonly restService: RestService) {}

  getSettings = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatDeliveryChargeSettingsDto>({
      method: 'GET',
      url: '/api/app/talabat-delivery-charges',
    }, { apiName: this.apiName, ...config });

  updateActive = (input: UpdateTalabatActiveDeliveryChargeInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatDeliveryChargeSettingsDto>({
      method: 'PUT',
      url: '/api/app/talabat-delivery-charges/active',
      body: input,
    }, { apiName: this.apiName, ...config });
}
