
export interface HangfireDashboardDto {
  counts: HangfireJobCountDto;
  succeededJobs: HangfireJobItemDto[];
  failedJobs: HangfireJobItemDto[];
  processingJobs: HangfireJobItemDto[];
  scheduledJobs: HangfireJobItemDto[];
  enqueuedJobs: HangfireJobItemDto[];
}

export interface HangfireJobCountDto {
  succeeded: number;
  failed: number;
  processing: number;
  scheduled: number;
  enqueued: number;
  awaitingRetry: number;
  deleted: number;
}

export interface HangfireJobItemDto {
  id?: string;
  state?: string;
  jobName?: string;
  queue?: string;
  createdAt?: string;
  enqueuedAt?: string;
  scheduledAt?: string;
  startedAt?: string;
  completedAt?: string;
  arguments?: string;
  exceptionMessage?: string;
}

export interface GetStagingMenuGroupSummaryRequest {
  foodicsAccountId?: string;
  branchId?: string;
}

export interface StagingMenuGroupSummaryDto {
  groupId?: string;
  groupName?: string;
  totalProducts?: number;
  totalCategories?: number;
  isMappedToTalabatAccount?: boolean;
}

export interface FoodicsGroupWithProductCountDto {
  id?: string;
  name?: string;
  nameLocalized?: string;
  productCount?: number;
}