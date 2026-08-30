import { inject } from '@angular/core';
import { CanActivateFn, GuardResult, MaybeAsync, Router } from '@angular/router';
import { TokenStorage } from './token-storage';

/**
 * UX gate for authenticated admin routes (KYC-061).
 * API JWT enforcement remains the real security boundary.
 */
export const authGuard: CanActivateFn = (_route, state): MaybeAsync<GuardResult> => {
  const tokens: TokenStorage = inject(TokenStorage);
  const router: Router = inject(Router);

  if (tokens.getAccessToken()) {
    return true;
  }

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};

/** Sends already-authenticated users away from the login page. */
export const guestGuard: CanActivateFn = (): MaybeAsync<GuardResult> => {
  const tokens: TokenStorage = inject(TokenStorage);
  const router: Router = inject(Router);

  if (!tokens.getAccessToken()) {
    return true;
  }

  return router.createUrlTree(['/cases']);
};
