import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from '@abp/ng.core';
import { LANDING_POLICY } from '../account/no-access/no-access.component';

/**
 * Guards the default landing page. ABP's permissionGuard filters a denied policy out of its
 * observable, so navigation just stalls and the user is left on a dead page; here a user who
 * cannot open the dashboard is sent somewhere that explains why instead.
 */
export const landingGuard: CanActivateFn = () => {
  const permission = inject(PermissionService);
  const router = inject(Router);

  return permission.getGrantedPolicy(LANDING_POLICY) ? true : router.parseUrl('/no-access');
};
