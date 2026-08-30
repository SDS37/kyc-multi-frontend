import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, tap, throwError } from 'rxjs';
import { APP_CONFIG, AppConfig } from '../config/app-config';
import {
  normalizeLoginCredentials,
  parseLoginSuccess,
  toLoginFailedError,
} from './auth.mappers';
import {
  GraphqlLoginBody,
  LoginCredentials,
  LoginFailedError,
  LoginSuccess,
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
    const input: LoginCredentials = normalizeLoginCredentials(credentials);

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
        map((body: GraphqlLoginBody): LoginSuccess => parseLoginSuccess(body)),
        tap((login: LoginSuccess): void => {
          this.tokens.setAccessToken(login.accessToken);
        }),
        catchError((err: unknown): Observable<never> =>
          throwError((): LoginFailedError => toLoginFailedError(err)),
        ),
      );
  }
}
