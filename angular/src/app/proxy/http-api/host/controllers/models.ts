
export interface DeleteItemsRequest {
  remoteCodesToDelete: string[];
}

export interface HealthCheckResult {
  status?: string;
  timestamp?: string;
  services: ServiceHealthStatus;
}

export interface KafkaTestResult {
  status?: string;
  message?: string;
  timestamp?: string;
  errorDetails?: string;
}

export interface ServiceHealthStatus {
  eventBus?: string;
}
