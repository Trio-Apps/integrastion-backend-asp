import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { RestService } from '@abp/ng.core';
import { catchError, map, of } from 'rxjs';

interface PasswordChangeRequiredResponse {
  required: boolean;
}

export const passwordChangeRequiredGuard: CanActivateFn = (_, state) => {
  const restService = inject(RestService);
  const router = inject(Router);

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
      { apiName: 'Default' }
    )
    .pipe(
      map(response => (response?.required ? router.createUrlTree(['/account/force-change-password']) : true)),
      catchError(() => of(true))
    );
};
