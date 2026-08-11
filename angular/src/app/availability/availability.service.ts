import { Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

export interface AvailabilityItemDto {
  foodicsProductId: string;
  name: string;
  nameLocalized?: string;
  sku?: string;
  categoryName?: string;
  vendorCode: string;
  isInStock: boolean;
  mode?: string;
  restoreAtUtc?: string;
}

export interface GetAvailabilityInput {
  search?: string;
  vendorCode?: string;
  maxResultCount?: number;
  skipCount?: number;
}

export interface SetAvailabilityInput {
  foodicsProductIds: string[];
  vendorCodes: string[];
  inStock: boolean;
  mode?: string;
}

export interface AvailabilitySettingsDto {
  dayEndTime: string;
  timeZone: string;
  nextRestoreAtUtc: string;
}

export interface UpdateAvailabilitySettingsInput {
  dayEndTime: string;
  timeZone: string;
}

@Injectable({ providedIn: 'root' })
export class AvailabilityService {
  apiName = 'Default';

  constructor(private restService: RestService) {}

  getItems = (input: GetAvailabilityInput) =>
    this.restService.request<any, PagedResultDto<AvailabilityItemDto>>(
      { method: 'GET', url: '/api/app/availability/items', params: { ...input } },
      { apiName: this.apiName },
    );

  setAvailability = (input: SetAvailabilityInput) =>
    this.restService.request<any, void>(
      { method: 'POST', url: '/api/app/availability/set-availability', body: input },
      { apiName: this.apiName },
    );

  getSettings = () =>
    this.restService.request<any, AvailabilitySettingsDto>(
      { method: 'GET', url: '/api/app/availability/settings' },
      { apiName: this.apiName },
    );

  updateSettings = (input: UpdateAvailabilitySettingsInput) =>
    this.restService.request<any, AvailabilitySettingsDto>(
      { method: 'PUT', url: '/api/app/availability/settings', body: input },
      { apiName: this.apiName },
    );
}
