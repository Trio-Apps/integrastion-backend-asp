import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';

export interface TalabatPaymentMethodDto {
  id: string;
  name: string;
  nameLocalized?: string;
  code?: string;
  type?: number;
  isActive: boolean;
}

export interface TalabatPaymentMethodSettingsDto {
  activePaymentMethodId?: string;
  activePaymentMethodName?: string;
  activePaymentMethodCode?: string;
  source?: string;
  paymentMethods: TalabatPaymentMethodDto[];
}

export interface UpdateTalabatActivePaymentMethodInput {
  paymentMethodId?: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class TalabatPaymentMethodsService {
  apiName = 'Default';

  constructor(private readonly restService: RestService) {}

  getSettings = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatPaymentMethodSettingsDto>({
      method: 'GET',
      url: '/api/app/talabat-payment-methods',
    }, { apiName: this.apiName, ...config });

  updateActive = (input: UpdateTalabatActivePaymentMethodInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatPaymentMethodSettingsDto>({
      method: 'PUT',
      url: '/api/app/talabat-payment-methods/active',
      body: input,
    }, { apiName: this.apiName, ...config });
}
