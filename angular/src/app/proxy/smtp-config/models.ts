import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateUpdateSmtpConfigDto {
  host?: string;
  port?: number;
  userName?: string;
  password?: string;
  fromName?: string;
  fromEmail?: string;
  enableSsl?: boolean;
  useStartTls?: boolean;
}

export interface SmtpConfigDto extends FullAuditedEntityDto<string> {
  host?: string;
  port?: number;
  userName?: string;
  password?: string;
  fromName?: string;
  fromEmail?: string;
  enableSsl?: boolean;
  useStartTls?: boolean;
}

export interface SmtpTestResultDto {
  success?: boolean;
  message?: string;
}
