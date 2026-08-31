import { inject } from '@angular/core';
import { CanActivateFn, GuardResult, MaybeAsync, Router } from '@angular/router';
import { toShellSession } from './auth.mappers';
import { TokenStorage } from './token-storage';

/**
 * UX gate for authenticated admin routes.
 * Requires a parseable JWT (clears corrupt tokens). API JWT remains the security boundary.
 */
export const authGuard: CanActivateFn = (_route, state): MaybeAsync<GuardResult> => {
  const tokens: TokenStorage = inject(TokenStorage);
  const router: Router = inject(Router);

  if (hasValidSession(tokens)) {
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

  if (!hasValidSession(tokens)) {
    return true;
  }

  return router.createUrlTree(['/cases']);
};

function hasValidSession(tokens: TokenStorage): boolean {
  const accessToken: string | null = tokens.getAccessToken();
  if (!accessToken) {
    return false;
  }
  const session = toShellSession(accessToken, tokens.getTenantSlug());
  if (!session) {
    tokens.clearSession();
    return false;
  }
  return true;
}
