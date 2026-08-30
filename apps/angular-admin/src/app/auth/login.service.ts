import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { SKIP_AUTH } from './skip-auth';
import { TokenStorage } from './token-storage';

const LOGIN_MUTATION = `
  mutation Login($input: LoginRequestInput!) {
    login(input: $input) {
      accessToken
      tokenType
      expiresInSeconds
    }
  }
`;

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

interface GraphqlError {
  message?: string;
  extensions?: { code?: string };
}

interface GraphqlLoginBody {
  data?: { login?: LoginSuccess | null };
  errors?: GraphqlError[];
}

/**
 * Anonymous GraphQL `login` against the shared API (KYC-061).
 * Writes the JWT into TokenStorage on success; uses SKIP_AUTH so the interceptor stays quiet.
 */
@Injectable({ providedIn: 'root' })
export class LoginService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);
  private readonly tokens = inject(TokenStorage);

  login(credentials: LoginCredentials): Observable<LoginSuccess> {
    return this.http
      .post<GraphqlLoginBody>(
        this.config.graphqlUrl,
        {
          query: LOGIN_MUTATION,
          variables: {
            input: {
              tenantSlug: credentials.tenantSlug.trim(),
              email: credentials.email.trim(),
              password: credentials.password,
            },
          },
        },
        { context: new HttpContext().set(SKIP_AUTH, true) },
      )
      .pipe(
        map((body) => {
          const gqlError = body.errors?.[0];
          if (gqlError) {
            throw new LoginFailedError(
              gqlError.message?.trim() || 'Sign-in failed. Check your details and try again.',
              gqlError.extensions?.code,
            );
          }

          const login = body.data?.login;
          if (!login?.accessToken) {
            throw new LoginFailedError('Sign-in failed. Check your details and try again.');
          }

          this.tokens.setAccessToken(login.accessToken);
          return login;
        }),
        catchError((err: unknown) => {
          if (err instanceof LoginFailedError) {
            return throwError(() => err);
          }
          if (err instanceof HttpErrorResponse) {
            return throwError(
              () =>
                new LoginFailedError(
                  'Unable to reach the sign-in service. Try again in a moment.',
                  'NETWORK',
                ),
            );
          }
          return throwError(
            () => new LoginFailedError('Sign-in failed. Check your details and try again.'),
          );
        }),
      );
  }
}
