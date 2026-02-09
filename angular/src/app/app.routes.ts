import { authGuard, permissionGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';
import { GDPR_COOKIE_CONSENT_ROUTES } from './gdpr-cookie-consent/gdpr-cookie-consent.routes';
import { passwordChangeRequiredGuard } from './guards/password-change-required.guard';

export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard',
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/dashboard.component').then(c => c.DashboardComponent),
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Host || OrderXChange.Dashboard.Tenant',
    },
  },
  {
    path: 'hangfire-monitoring',
    loadComponent: () => import('./hangfire-dashboard/hangfire-dashboard.component').then(c => c.HangfireDashboardComponent),
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Host || OrderXChange.Dashboard.Tenant',
    },
  },
  {
    path: 'talabat-dashboard',
    loadComponent: () => import('./talabat-dashboard/talabat-dashboard.component').then(c => c.TalabatDashboardComponent),
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
  },
  {
    path: 'talabat-orders',
    loadComponent: () => import('./talabat-orders/talabat-orders.component').then(c => c.TalabatOrdersComponent),
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
  },
  {
    path: 'categories',
    loadComponent: () => import('./menu-demo/menu-demo.component').then(c => c.MenuDemoComponent),
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
  },
  {
    path: 'account/force-change-password',
    loadComponent: () =>
      import('./account/force-change-password/force-change-password.component').then(
        c => c.ForceChangePasswordComponent
      ),
    canActivate: [authGuard],
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./account/forgot-password/forgot-password.component').then(
        c => c.ForgotPasswordComponent
      ),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./account/reset-password/reset-password.component').then(
        c => c.ResetPasswordComponent
      ),
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
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Host',
    },
  },
  {
    path: 'saas/smtp-config',
    loadComponent: () => import('./saas/smtp-config/smtp-config.component').then(c => c.SmtpConfigComponent),
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Host',
    },
  },
  {
    path: 'foodics',
    loadComponent: () => import('./saas/foodics-list.component/foodics-list.component').then(c => c.FoodicsListComponent),
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
  },
  {
    path: 'talabat',
    loadComponent: () => import('./saas/talabat-list.component/talabat-list.component').then(c => c.TalabatListComponent),
    canActivate: [authGuard, passwordChangeRequiredGuard, permissionGuard],
    data: {
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
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
