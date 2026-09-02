import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, tap, throwError } from 'rxjs';
import { APP_CONFIG, AppConfig } from '../config/app-config';
import {
  parseLoginSuccess,
  toLoginFailedError,
  toLoginMutationInput,
  toShellSession,
} from './auth.mappers';
import { LOGIN_MESSAGES } from './auth.messages';
import {
  GraphqlLoginBody,
  LoginCredentials,
  LoginFailedError,
  LoginMutationInput,
  LoginSuccess,
  ShellSession,
  isAdminRole,
} from './auth.models';
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

/**
 * Anonymous GraphQL `login` against the shared API (KYC-061).
 * Pure parse/normalize in `auth.mappers`; token write is the intentional side effect here.
 */
@Injectable({ providedIn: 'root' })
export class LoginService {
  private readonly http: HttpClient = inject(HttpClient);
  private readonly config: AppConfig = inject(APP_CONFIG);
  private readonly tokens: TokenStorage = inject(TokenStorage);

  login(credentials: LoginCredentials): Observable<LoginSuccess> {
    const input: LoginMutationInput = toLoginMutationInput(credentials);

    return this.http
      .post<GraphqlLoginBody>(
        this.config.graphqlUrl,
        {
          query: LOGIN_MUTATION,
          variables: { input },
        },
        { context: new HttpContext().set(SKIP_AUTH, true) },
      )
      .pipe(
        map((body: GraphqlLoginBody): LoginSuccess => {
          const login: LoginSuccess = parseLoginSuccess(body);
          const session: ShellSession | null = toShellSession(
            login.accessToken,
            input.tenantSlug,
          );
          if (session === null || !isAdminRole(session.role)) {
            throw new LoginFailedError(LOGIN_MESSAGES.wrongAppRole, 'AUTH_NOT_AUTHORIZED');
          }
          return login;
        }),
        tap((login: LoginSuccess): void => {
          this.tokens.setSession(login.accessToken, input.tenantSlug);
        }),
        catchError((err: unknown): Observable<never> =>
          throwError((): LoginFailedError => toLoginFailedError(err)),
        ),
      );
  }
}
