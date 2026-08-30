import { HttpErrorResponse } from '@angular/common/http';
import { GraphqlError } from '../shared/graphql.models';
import {
  AccessTokenClaims,
  AppRole,
  GraphqlLoginBody,
  LoginCredentials,
  LoginFailedError,
  LoginSuccess,
  ShellSession,
  isAppRole,
} from './auth.models';

const DEFAULT_POST_LOGIN_URL: string = '/cases';

/** Pure: trim slug/email; password unchanged. */
export function normalizeLoginCredentials(credentials: LoginCredentials): LoginCredentials {
  return {
    tenantSlug: credentials.tenantSlug.trim(),
    email: credentials.email.trim(),
    password: credentials.password,
  };
}

/** Pure: map GraphQL login body → success DTO (throws LoginFailedError on bad payload). */
export function parseLoginSuccess(body: GraphqlLoginBody): LoginSuccess {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw new LoginFailedError(
      gqlError.message?.trim() || 'Sign-in failed. Check your details and try again.',
      gqlError.extensions?.code,
    );
  }

  const login: LoginSuccess | null | undefined = body.data?.login;
  if (!login?.accessToken) {
    throw new LoginFailedError('Sign-in failed. Check your details and try again.');
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
  if (err instanceof HttpErrorResponse) {
    return new LoginFailedError(
      'Unable to reach the sign-in service. Try again in a moment.',
      'NETWORK',
    );
  }
  return new LoginFailedError('Sign-in failed. Check your details and try again.');
}

/** Pure: human-readable role label for the shell. */
export function appRoleLabel(role: AppRole): string {
  switch (role) {
    case 'TenantAdmin':
      return 'Tenant admin';
    case 'Reviewer':
      return 'Reviewer';
    case 'Customer':
      return 'Customer';
  }
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
