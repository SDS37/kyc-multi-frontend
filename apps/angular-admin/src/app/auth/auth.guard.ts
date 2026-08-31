import { inject } from '@angular/core';
import { CanActivateFn, GuardResult, MaybeAsync, Router } from '@angular/router';
import { toShellSession } from './auth.mappers';
import { AppRole, ShellSession } from './auth.models';
import { TokenStorage } from './token-storage';

/** Admin portal personas (Customer uses react-customer). */
const ADMIN_ROLES: readonly AppRole[] = ['Reviewer', 'TenantAdmin'];

/**
 * UX gate for authenticated admin routes.
 * Requires a parseable, unexpired JWT with Reviewer|TenantAdmin.
 * API JWT remains the security boundary.
 */
export const authGuard: CanActivateFn = (_route, state): MaybeAsync<GuardResult> => {
  const tokens: TokenStorage = inject(TokenStorage);
  const router: Router = inject(Router);

  const session: ShellSession | null = readValidAdminSession(tokens);
  if (session) {
    return true;
  }

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};

/** Sends already-authenticated admin users away from the login page. */
export const guestGuard: CanActivateFn = (): MaybeAsync<GuardResult> => {
  const tokens: TokenStorage = inject(TokenStorage);
  const router: Router = inject(Router);

  if (!readValidAdminSession(tokens)) {
    return true;
  }

  return router.createUrlTree(['/cases']);
};

function readValidAdminSession(tokens: TokenStorage): ShellSession | null {
  const accessToken: string | null = tokens.getAccessToken();
  if (!accessToken) {
    return null;
  }
  const session: ShellSession | null = toShellSession(accessToken, tokens.getTenantSlug());
  if (!session) {
    tokens.clearSession();
    return null;
  }
  if (!ADMIN_ROLES.includes(session.role)) {
    tokens.clearSession();
    return null;
  }
  return session;
}
