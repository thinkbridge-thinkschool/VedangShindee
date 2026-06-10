import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const authGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  const token  = localStorage.getItem('access_token');
  return token
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
