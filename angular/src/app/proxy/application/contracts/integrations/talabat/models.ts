
export interface TalabatBranchAvailabilityUpdate {
  vendorCode?: string;
  items: TalabatBranchItemAvailability[];
}

export interface TalabatBranchItemAvailability {
  remoteCode?: string;
  type?: string;
  isAvailable: boolean;
  price?: number;
  availableAt?: string;
  reason?: string;
}

export interface TalabatBranchItemAvailabilityRequest {
  items: TalabatBranchItemAvailability[];
}

export interface TalabatMultiBranchAvailabilityRequest {
  branches: TalabatBranchAvailabilityUpdate[];
}
