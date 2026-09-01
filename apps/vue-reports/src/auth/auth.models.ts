import type { GraphqlError } from '../shared/graphql.models';

export interface LoginCredentials {
  tenantSlug: string;
  email: string;
  password: string;
}

export interface LoginSuccess {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
}

/** JWT `role` claim values issued by the API. */
export type AppRole = 'TenantAdmin' | 'Reviewer' | 'Customer';

export const APP_ROLES: readonly AppRole[] = ['TenantAdmin', 'Reviewer', 'Customer'];

export function isAppRole(value: string): value is AppRole {
  return (APP_ROLES as readonly string[]).includes(value);
}

/** Roles allowed into the reports app (KYC-080). Customer uses React. */
export const REPORTS_ROLES: readonly AppRole[] = ['TenantAdmin', 'Reviewer'];

export function isReportsRole(role: AppRole): boolean {
  return REPORTS_ROLES.includes(role);
}

/** Display-only claims from the access token (never trust for authorization). */
export interface AccessTokenClaims {
  subject: string;
  tenantId: string;
  role: AppRole;
  email: string;
}

/** Shell header session (JWT claims + login tenant slug). */
export interface ShellSession {
  tenantSlug: string | null;
  tenantId: string;
  email: string;
  role: AppRole;
}

/** Per-field validation messages for the login form. */
export interface LoginFieldErrors {
  readonly tenantSlug?: string;
  readonly email?: string;
  readonly password?: string;
}

/** User-facing login failure (validation, AUTH_FAILED, or transport). */
export class LoginFailedError extends Error {
  readonly code?: string;

  constructor(message: string, code?: string) {
    super(message);
    this.name = 'LoginFailedError';
    this.code = code;
  }
}

/** Wire shape for GraphQL `login` HTTP body (auth feature only). */
export interface GraphqlLoginBody {
  data?: { login?: LoginSuccess | null };
  errors?: GraphqlError[];
}

/** Vue Router meta used by the reports guard. */
export interface ReportsRouteMeta {
  readonly requiresAuth?: boolean;
  readonly guestOnly?: boolean;
}

/** Redirect produced by the navigation guard (null = continue). */
export interface ReportsNavigationRedirect {
  readonly path: string;
  readonly query?: { readonly returnUrl: string };
  readonly replace: true;
  readonly clearSession: boolean;
}
