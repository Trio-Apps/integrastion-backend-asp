import { Environment } from '@abp/ng.core';

const baseUrl = 'http://57.128.145.20';

const oAuthConfig = {
  issuer: 'http://57.128.145.20/',
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
  production: true,
  application: {
    baseUrl,
    name: 'OrderXChange',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'http://57.128.145.20',
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
