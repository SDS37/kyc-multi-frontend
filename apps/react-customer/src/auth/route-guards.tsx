import type { ReactElement } from 'react';
import { Navigate, useLocation } from 'react-router';
import { getValidShellSession } from './session';

/** Redirect guests to login with a safe returnUrl (Angular authGuard parity). */
export function RequireAuth({ children }: { readonly children: ReactElement }): ReactElement {
  const location = useLocation();
  const session = getValidShellSession();

  if (!session) {
    const returnUrl: string = `${location.pathname}${location.search}`;
    const search: string = `?returnUrl=${encodeURIComponent(returnUrl)}`;
    return <Navigate to={`/login${search}`} replace />;
  }

  return children;
}

/** Redirect authenticated users away from guest-only routes (Angular guestGuard parity). */
export function RequireGuest({ children }: { readonly children: ReactElement }): ReactElement {
  const session = getValidShellSession();
  if (session) {
    return <Navigate to="/cases" replace />;
  }
  return children;
}
