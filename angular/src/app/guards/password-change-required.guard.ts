import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, RestService } from '@abp/ng.core';
import { catchError, map, of } from 'rxjs';

interface PasswordChangeRequiredResponse {
  required: boolean;
}

export const passwordChangeRequiredGuard: CanActivateFn = (_, state) => {
  const authService = inject(AuthService);
  const restService = inject(RestService);
  const router = inject(Router);

  if (!authService.isAuthenticated) {
    return of(true);
  }

  // Avoid a redirect loop when user is already on the forced change page.
  if (state.url.startsWith('/account/force-change-password')) {
    return of(true);
  }

  return restService
    .request<any, PasswordChangeRequiredResponse>(
      {
        method: 'GET',
        url: '/api/account/password/change-required',
      },
      { apiName: 'default' }
    )
    .pipe(
      map(response => (response?.required ? router.createUrlTree(['/account/force-change-password']) : true)),
      catchError(() => of(true))
    );
};
