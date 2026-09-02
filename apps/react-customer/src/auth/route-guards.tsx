import type { ReactElement } from 'react';
import { useEffect } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { loginPathWithReturnUrl } from './auth.mappers';
import type { AppRole, ShellSession } from './auth.models';
import { onSessionCleared } from './session-events';
import { getValidShellSession } from './session';
import { tokenStorage } from './token-storage';

const CUSTOMER_ROLES: readonly AppRole[] = ['Customer'];

/** Redirect guests to login with a safe returnUrl (Angular authGuard parity). */
export function RequireAuth({ children }: { readonly children: ReactElement }): ReactElement {
  const location = useLocation();
  const navigate = useNavigate();
  const session: ShellSession | null = getValidShellSession();

  useEffect(() => {
    return onSessionCleared((): void => {
      const returnUrl: string = `${location.pathname}${location.search}`;
      if (!location.pathname.startsWith('/login')) {
        void navigate(loginPathWithReturnUrl(returnUrl), { replace: true });
      }
    });
  }, [location.pathname, location.search, navigate]);

  if (!session) {
    const returnUrl: string = `${location.pathname}${location.search}`;
    return <Navigate to={loginPathWithReturnUrl(returnUrl)} replace />;
  }

  if (!CUSTOMER_ROLES.includes(session.role)) {
    tokenStorage.clearSession();
    const returnUrl: string = `${location.pathname}${location.search}`;
    return <Navigate to={loginPathWithReturnUrl(returnUrl)} replace />;
  }

  return children;
}

/** Redirect authenticated customers away from guest-only routes (Angular guestGuard parity). */
export function RequireGuest({ children }: { readonly children: ReactElement }): ReactElement {
  const session: ShellSession | null = getValidShellSession();
  if (session && CUSTOMER_ROLES.includes(session.role)) {
    return <Navigate to="/cases" replace />;
  }
  if (session) {
    tokenStorage.clearSession();
  }
  return children;
}
