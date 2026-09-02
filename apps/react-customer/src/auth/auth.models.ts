import type { GraphqlError } from '../shared/graphql.models';

export interface LoginCredentials {
  tenantSlug: string;
  email: string;
  password: string;
  captchaToken?: string;
}

/** GraphQL `login` variables. Omit `captchaToken` when the user did not supply one. */
export interface LoginMutationInput {
  readonly tenantSlug: string;
  readonly email: string;
  readonly password: string;
  readonly captchaToken?: string;
}

export const RATE_LIMITED_HTTP_STATUS: number = 429;
export const RATE_LIMITED_CODE: string = 'RATE_LIMITED';

export interface LoginSuccess {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
}

/** JWT `role` claim values issued by the API. */
export type AppRole = 'TenantAdmin' | 'Reviewer' | 'Customer';

export const APP_ROLES: readonly AppRole[] = [
  'TenantAdmin',
  'Reviewer',
  'Customer',
];

export function isAppRole(value: string): value is AppRole {
  return (APP_ROLES as readonly string[]).includes(value);
}

/** Roles allowed into the customer app (KYC-071). Reviewers use Angular / Vue. */
export const CUSTOMER_ROLES: readonly AppRole[] = ['Customer'];

export function isCustomerRole(role: AppRole): boolean {
  return CUSTOMER_ROLES.includes(role);
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
  readonly captchaToken?: string;
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
