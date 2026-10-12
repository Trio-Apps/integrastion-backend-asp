import { Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';

export interface TalabatOrderDiscountSponsorshipDto {
  sponsor?: string;
  amount?: number;
}
export interface TalabatOrderItemDiscountDto {
  name?: string;
  amount?: number;
  sponsorships: TalabatOrderDiscountSponsorshipDto[];
}
export interface TalabatOrderModifierDto {
  name?: string;
  remoteCode?: string;
  quantity: number;
  price?: number;
  discountAmount?: number;
  discounts: TalabatOrderItemDiscountDto[];
}
export interface TalabatOrderItemDto {
  name?: string;
  categoryName?: string;
  remoteCode?: string;
  quantity: number;
  unitPrice?: number;
  paidPrice?: number;
  discountAmount?: number;
  discounts: TalabatOrderItemDiscountDto[];
  modifiers: TalabatOrderModifierDto[];
}
export interface TalabatOrderDetailsDto {
  id: string;
  orderCode?: string;
  orderToken?: string;
  shortCode?: string;
  vendorCode?: string;
  status?: string;
  orderCreatedAt?: string;
  receivedAt?: string;
  customerId?: string;
  customerName?: string;
  customerPhone?: string;
  customerAddress?: string;
  paymentMethod?: string;
  expeditionType?: string;
  channel?: string;
  grandTotal?: number;
  discountTotal?: number;
  customerComment?: string;
  items: TalabatOrderItemDto[];
  lastError?: string;
  attempts: number;
  foodicsOrderId?: string;
  rawPayloadJson?: string;
}

@Injectable({ providedIn: 'root' })
export class OrderDetailService {
  apiName = 'Default';

  constructor(private restService: RestService) {}

  getDetails = (id: string) =>
    this.restService.request<any, TalabatOrderDetailsDto>(
      { method: 'GET', url: `/api/app/talabat-order-log/${id}/details` },
      { apiName: this.apiName },
    );
}
