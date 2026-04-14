import { Rest, RestService } from '@abp/ng.core';
import { Injectable } from '@angular/core';

export interface TalabatCustomerMappingAccountDto {
  talabatAccountId: string;
  name: string;
  vendorCode: string;
  foodicsAccountId?: string;
  foodicsAccountName?: string;
  defaultCustomerId?: string;
  defaultCustomerName?: string;
  defaultCustomerAddressId?: string;
  defaultCustomerAddressName?: string;
}

export interface TalabatCustomerMappingSettingsDto {
  accounts: TalabatCustomerMappingAccountDto[];
}

export interface FoodicsCustomerLookupDto {
  id: string;
  name?: string;
  phone?: string;
  email?: string;
  dialCode?: number;
}

export interface FoodicsAddressLookupDto {
  id: string;
  name?: string;
  description?: string;
}

export interface UpdateTalabatDefaultCustomerMappingInput {
  talabatAccountId: string;
  customerId?: string | null;
  customerName?: string | null;
  customerAddressId?: string | null;
  customerAddressName?: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class TalabatCustomerMappingService {
  apiName = 'Default';

  constructor(private readonly restService: RestService) {}

  getSettings = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatCustomerMappingSettingsDto>({
      method: 'GET',
      url: '/api/app/talabat-customer-mapping',
    }, { apiName: this.apiName, ...config });

  searchCustomers = (talabatAccountId: string, filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsCustomerLookupDto[]>({
      method: 'GET',
      url: '/api/app/talabat-customer-mapping/customers',
      params: { talabatAccountId, filter },
    }, { apiName: this.apiName, ...config });

  getAddresses = (talabatAccountId: string, customerId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FoodicsAddressLookupDto[]>({
      method: 'GET',
      url: '/api/app/talabat-customer-mapping/addresses',
      params: { talabatAccountId, customerId },
    }, { apiName: this.apiName, ...config });

  update = (input: UpdateTalabatDefaultCustomerMappingInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TalabatCustomerMappingSettingsDto>({
      method: 'PUT',
      url: '/api/app/talabat-customer-mapping',
      body: input,
    }, { apiName: this.apiName, ...config });
}
