import { authGuard, permissionGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';
import { GDPR_COOKIE_CONSENT_ROUTES } from './gdpr-cookie-consent/gdpr-cookie-consent.routes';

export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home.component').then(c => c.HomeComponent),
    canActivate : [authGuard , permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Host || OrderXChange.Dashboard.Tenant',
    },
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/dashboard.component').then(c => c.DashboardComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'hangfire-monitoring',
    loadComponent: () => import('./hangfire-dashboard/hangfire-dashboard.component').then(c => c.HangfireDashboardComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Host',
    },
  },
  {
    path: 'talabat-dashboard',
    loadComponent: () => import('./talabat-dashboard/talabat-dashboard.component').then(c => c.TalabatDashboardComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'talabat-orders',
    loadComponent: () => import('./talabat-orders/talabat-orders.component').then(c => c.TalabatOrdersComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'categories',
    loadComponent: () => import('./menu-demo/menu-demo.component').then(c => c.MenuDemoComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
  },
  {
    path: 'account',
    loadChildren: () => import('@volo/abp.ng.account/public').then(c => c.createRoutes()),
  },
  {
    path: 'gdpr',
    loadChildren: () => import('@volo/abp.ng.gdpr').then(c => c.createRoutes()),
  },
  {
    path: 'identity',
    loadChildren: () => import('@volo/abp.ng.identity').then(c => c.createRoutes()),
  },
  {
    path: 'language-management',
    loadChildren: () => import('@volo/abp.ng.language-management').then(c => c.createRoutes()),
  },
  {
    path: 'saas/tenants',
    loadComponent: () => import('./saas/tenant-list.component/tenant-list.component').then(c => c.TenantListComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'saas/smtp-config',
    loadComponent: () => import('./saas/smtp-config/smtp-config.component').then(c => c.SmtpConfigComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'foodics',
    loadComponent: () => import('./saas/foodics-list.component/foodics-list.component').then(c => c.FoodicsListComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'talabat',
    loadComponent: () => import('./saas/talabat-list.component/talabat-list.component').then(c => c.TalabatListComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'audit-logs',
    loadChildren: () => import('@volo/abp.ng.audit-logging').then(c => c.createRoutes()),
  },
  {
    path: 'openiddict',
    loadChildren: () => import('@volo/abp.ng.openiddictpro').then(c => c.createRoutes()),
  },
  {
    path: 'text-template-management',
    loadChildren: () => import('@volo/abp.ng.text-template-management').then(c => c.createRoutes()),
  },
  {
    path: 'gdpr-cookie-consent',
    children: GDPR_COOKIE_CONSENT_ROUTES,
  },
  {
    path: 'setting-management',
    loadChildren: () => import('@abp/ng.setting-management').then(c => c.createRoutes()),
  },
];
