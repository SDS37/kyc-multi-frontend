import { HttpErrorResponse } from '@angular/common/http';
import { GraphqlError } from '../shared/graphql.models';
import {
  GraphqlLoginBody,
  LoginCredentials,
  LoginFailedError,
  LoginSuccess,
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
