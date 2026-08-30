import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { APP_CONFIG, AppConfig } from '../config/app-config';
import { SKIP_AUTH } from './skip-auth';
import { TokenStorage } from './token-storage';

const LOGIN_MUTATION: string = `
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
  private readonly http: HttpClient = inject(HttpClient);
  private readonly config: AppConfig = inject(APP_CONFIG);
  private readonly tokens: TokenStorage = inject(TokenStorage);

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
        map((body: GraphqlLoginBody): LoginSuccess => {
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

          this.tokens.setAccessToken(login.accessToken);
          return login;
        }),
        catchError((err: unknown): Observable<never> => {
          if (err instanceof LoginFailedError) {
            return throwError((): LoginFailedError => err);
          }
          if (err instanceof HttpErrorResponse) {
            return throwError(
              (): LoginFailedError =>
                new LoginFailedError(
                  'Unable to reach the sign-in service. Try again in a moment.',
                  'NETWORK',
                ),
            );
          }
          return throwError(
            (): LoginFailedError =>
              new LoginFailedError('Sign-in failed. Check your details and try again.'),
          );
        }),
      );
  }
}
