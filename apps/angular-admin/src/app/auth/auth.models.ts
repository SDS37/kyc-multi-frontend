import { FormControl } from '@angular/forms';
import { GraphqlError } from '../shared/graphql.models';

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

/** Reactive form controls for the login page. */
export interface LoginFormControls {
  tenantSlug: FormControl<string>;
  email: FormControl<string>;
  password: FormControl<string>;
}

/** JWT `role` claim values issued by the API. */
export const APP_ROLES = ['TenantAdmin', 'Reviewer', 'Customer'] as const;

export type AppRole = (typeof APP_ROLES)[number];

export function isAppRole(value: string): value is AppRole {
  return (APP_ROLES as readonly string[]).includes(value);
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

/** User-facing login failure (validation, AUTH_FAILED, or transport). */
export class LoginFailedError extends Error {
  constructor(
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'LoginFailedError';
  }
}

/** Wire shape for GraphQL `login` HTTP body (auth feature only). */
export interface GraphqlLoginBody {
  data?: { login?: LoginSuccess | null };
  errors?: GraphqlError[];
}
