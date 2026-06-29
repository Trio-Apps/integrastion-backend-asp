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
  // URLs above are LIVE defaults. Each server can override them at runtime by
  // serving its own /dynamic-env.json (deep-merged over these). The testing
  // server mounts a dynamic-env.json with the -dev URLs (see docker-compose.override.yml.example).
  remoteEnv: {
    url: '/dynamic-env.json',
    mergeStrategy: 'deepmerge'
  }
} as Environment;
