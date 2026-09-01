import type { GraphqlError } from '../shared/graphql.models';
import type { GraphqlResponse } from '../shared/graphql.models';
import { LOGIN_MESSAGES } from './auth.messages';
import {
  type AccessTokenClaims,
  type GraphqlLoginBody,
  type LoginCredentials,
  type LoginFieldErrors,
  LoginFailedError,
  type LoginSuccess,
  type ReportsNavigationRedirect,
  type ReportsRouteMeta,
  type ShellSession,
  isAppRole,
  isReportsRole,
} from './auth.models';

export { appRoleLabel } from './auth.messages';

const DEFAULT_POST_LOGIN_URL: string = '/reports';
const EMAIL_PATTERN: RegExp = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Pure: trim slug/email; password unchanged. */
export function normalizeLoginCredentials(credentials: LoginCredentials): LoginCredentials {
  return {
    tenantSlug: credentials.tenantSlug.trim(),
    email: credentials.email.trim(),
    password: credentials.password,
  };
}

/** Pure: client-side field validation (mirrors Angular/React login validators). */
export function validateLoginForm(credentials: LoginCredentials): LoginFieldErrors {
  const errors: {
    tenantSlug?: string;
    email?: string;
    password?: string;
  } = {};

  if (!credentials.tenantSlug.trim()) {
    errors.tenantSlug = LOGIN_MESSAGES.tenantSlugRequired;
  } else if (credentials.tenantSlug.trim().length > 64) {
    errors.tenantSlug = LOGIN_MESSAGES.tenantSlugMaxLength;
  }

  if (!credentials.email.trim()) {
    errors.email = LOGIN_MESSAGES.emailRequired;
  } else if (credentials.email.trim().length > 256) {
    errors.email = LOGIN_MESSAGES.emailMaxLength;
  } else if (!EMAIL_PATTERN.test(credentials.email.trim())) {
    errors.email = LOGIN_MESSAGES.emailInvalid;
  }

  if (!credentials.password) {
    errors.password = LOGIN_MESSAGES.passwordRequired;
  } else if (credentials.password.length > 128) {
    errors.password = LOGIN_MESSAGES.passwordMaxLength;
  }

  return errors;
}

export function hasLoginFieldErrors(errors: LoginFieldErrors): boolean {
  return (
    errors.tenantSlug !== undefined ||
    errors.email !== undefined ||
    errors.password !== undefined
  );
}

/** Pure: map GraphQL login body → success DTO (throws LoginFailedError on bad payload). */
export function parseLoginSuccess(
  body: GraphqlLoginBody | GraphqlResponse<{ login?: LoginSuccess | null }>,
): LoginSuccess {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw new LoginFailedError(
      gqlError.message?.trim() || LOGIN_MESSAGES.signInFailed,
      gqlError.extensions?.code,
    );
  }

  const login: LoginSuccess | null | undefined = body.data?.login;
  if (!login?.accessToken) {
    throw new LoginFailedError(LOGIN_MESSAGES.signInFailed);
  }

  return login;
}

/** Pure: safe in-app return URL after login (blocks open redirects). */
export function resolvePostLoginUrl(returnUrl: string | null): string {
  if (returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//')) {
    return returnUrl;
  }
  return DEFAULT_POST_LOGIN_URL;
}

/** Pure: map transport / unknown errors to LoginFailedError. */
export function toLoginFailedError(err: unknown): LoginFailedError {
  if (err instanceof LoginFailedError) {
    return err;
  }
  if (err instanceof TypeError) {
    return new LoginFailedError(LOGIN_MESSAGES.networkFailed, 'NETWORK');
  }
  if (err instanceof Error && /GraphQL HTTP|Failed to fetch|NetworkError/i.test(err.message)) {
    return new LoginFailedError(LOGIN_MESSAGES.networkFailed, 'NETWORK');
  }
  return new LoginFailedError(LOGIN_MESSAGES.signInFailed);
}

/**
 * Pure: decode JWT payload claims for shell display only.
 * Invalid tokens return null — never use this for authorization.
 */
export function parseAccessTokenClaims(accessToken: string): AccessTokenClaims | null {
  const parts: string[] = accessToken.split('.');
  if (parts.length < 2) {
    return null;
  }

  const payloadPart: string | undefined = parts[1];
  if (!payloadPart) {
    return null;
  }

  let json: string;
  try {
    json = decodeBase64Url(payloadPart);
  } catch {
    return null;
  }

  let payload: unknown;
  try {
    payload = JSON.parse(json) as unknown;
  } catch {
    return null;
  }

  if (payload === null || typeof payload !== 'object' || Array.isArray(payload)) {
    return null;
  }

  const record: Record<string, unknown> = payload as Record<string, unknown>;
  const subject: unknown = record['sub'];
  const tenantId: unknown = record['tenant_id'];
  const role: unknown = record['role'];
  const email: unknown = record['email'];

  if (
    typeof subject !== 'string' ||
    !subject ||
    typeof tenantId !== 'string' ||
    !tenantId ||
    typeof role !== 'string' ||
    !isAppRole(role) ||
    typeof email !== 'string' ||
    !email
  ) {
    return null;
  }

  const exp: unknown = record['exp'];
  if (typeof exp !== 'number' || !Number.isFinite(exp)) {
    return null;
  }
  if (exp * 1000 <= Date.now()) {
    return null;
  }

  return {
    subject,
    tenantId,
    role,
    email,
  };
}

/** Pure: combine JWT claims + stored tenant slug for the shell header. */
export function toShellSession(
  accessToken: string | null,
  tenantSlug: string | null,
): ShellSession | null {
  if (!accessToken) {
    return null;
  }
  const claims: AccessTokenClaims | null = parseAccessTokenClaims(accessToken);
  if (!claims) {
    return null;
  }
  const slug: string | null = tenantSlug?.trim() ? tenantSlug.trim() : null;
  return {
    tenantSlug: slug,
    tenantId: claims.tenantId,
    email: claims.email,
    role: claims.role,
  };
}

/**
 * Pure: Vue Router guard decision (KYC-080).
 * Returning a redirect does not authorize — the API still enforces JWT.
 */
export function resolveReportsNavigation(
  to: { readonly fullPath: string; readonly meta: ReportsRouteMeta },
  session: ShellSession | null,
): ReportsNavigationRedirect | null {
  const allowed: boolean = session !== null && isReportsRole(session.role);

  if (to.meta.requiresAuth === true && !allowed) {
    return {
      path: '/login',
      query: { returnUrl: to.fullPath },
      replace: true,
      clearSession: session !== null,
    };
  }

  if (to.meta.guestOnly === true && allowed) {
    return { path: '/reports', replace: true, clearSession: false };
  }

  return null;
}

function decodeBase64Url(value: string): string {
  const padded: string = value.replace(/-/g, '+').replace(/_/g, '/');
  const padLength: number = (4 - (padded.length % 4)) % 4;
  const base64: string = padded + '='.repeat(padLength);
  const binary: string = atob(base64);
  const bytes: Uint8Array = Uint8Array.from(binary, (char: string): number =>
    char.charCodeAt(0),
  );
  return new TextDecoder().decode(bytes);
}
