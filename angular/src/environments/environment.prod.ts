import { Environment } from '@abp/ng.core';

const baseUrl = 'https://tfconsole.beon-it.com';

const oAuthConfig = {
  issuer: 'https://tfapi.beon-it.com/',
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
  production: true,
  application: {
    baseUrl,
    name: 'OrderXChange',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://tfapi.beon-it.com',
      rootNamespace: 'OrderXChange',
    },
    Default: {
      url: 'https://tfapi.beon-it.com',
      rootNamespace: 'OrderXChange',
    },
    AbpTenantManagement: {
      url: 'https://tfapi.beon-it.com',
      rootNamespace: 'OrderXChange',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
  // remoteEnv: {
  //   url: '/getEnvConfig',
  //   mergeStrategy: 'deepmerge'
  // }
} as Environment;
