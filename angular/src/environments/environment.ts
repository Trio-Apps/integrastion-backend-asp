import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44325/',
  redirectUri: baseUrl,
  clientId: 'OrderXChange_App',
  scope: 'offline_access OrderXChange',
  requireHttps: true,
  impersonation: {
    tenantImpersonation: true,
    userImpersonation: true,
  }
};

export const environment = {
  production: false,
  application: {
    baseUrl,
    name: 'OrderXChange',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44325',
      rootNamespace: 'OrderXChange',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;
