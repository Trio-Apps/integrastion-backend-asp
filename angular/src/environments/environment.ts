import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4201';

const oAuthConfig = {
  issuer: 'http://localhost:8081/',
  redirectUri: baseUrl,
  clientId: 'OrderXChange_App',
  scope: 'offline_access OrderXChange',
  requireHttps: false,
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
      url: 'http://localhost:8081',
      rootNamespace: 'OrderXChange',
    },
    Default: {
      url: 'http://localhost:8081',
      rootNamespace: 'OrderXChange',
    },
    AbpTenantManagement: {
      url: 'http://localhost:8081',
      rootNamespace: 'OrderXChange',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;
