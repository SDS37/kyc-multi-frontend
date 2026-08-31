import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpRequest,
  HttpResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { APP_CONFIG, AppConfig } from '../config/app-config';
import { SKIP_AUTH } from './skip-auth';
import { TokenStorage } from './token-storage';

/**
 * Attaches `Authorization: Bearer <token>` for requests to the configured API origin only.
 * Clears the session on HTTP 401 or GraphQL AUTH_NOT_AUTHENTICATED.
 * Does not send tenant id headers (ADR-007). Skip with `SKIP_AUTH` for anonymous calls.
 * @see https://angular.dev/guide/http/interceptors
 */
export function authInterceptor(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> {
  if (req.context.get(SKIP_AUTH)) {
    return next(req);
  }

  const { apiBaseUrl }: AppConfig = inject(APP_CONFIG);
  if (!isConfiguredApiRequest(req.url, apiBaseUrl)) {
    return next(req);
  }

  const tokens: TokenStorage = inject(TokenStorage);
  const token: string | null = tokens.getAccessToken();
  const authorized: HttpRequest<unknown> = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorized).pipe(
    map((event: HttpEvent<unknown>): HttpEvent<unknown> => {
      if (event instanceof HttpResponse) {
        clearSessionOnGraphqlAuthFailure(event.body, tokens);
      }
      return event;
    }),
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401) {
        tokens.clearSession();
      }
      return throwError(() => err);
    }),
  );
}

/** True when the request targets the configured .NET API (GraphQL or REST under apiBaseUrl). */
export function isConfiguredApiRequest(requestUrl: string, apiBaseUrl: string): boolean {
  const base: string = apiBaseUrl.replace(/\/$/, '');
  try {
    const origin: string =
      typeof window !== 'undefined' ? window.location.origin : base;
    const absolute: URL = new URL(requestUrl, origin);
    return absolute.href === base || absolute.href.startsWith(`${base}/`);
  } catch {
    return false;
  }
}

function clearSessionOnGraphqlAuthFailure(body: unknown, tokens: TokenStorage): void {
  if (body === null || typeof body !== 'object' || Array.isArray(body)) {
    return;
  }
  const errors: unknown = (body as Record<string, unknown>)['errors'];
  if (!Array.isArray(errors)) {
    return;
  }
  const hasAuthFailure: boolean = errors.some((entry: unknown): boolean => {
    if (entry === null || typeof entry !== 'object' || Array.isArray(entry)) {
      return false;
    }
    const extensions: unknown = (entry as Record<string, unknown>)['extensions'];
    if (extensions === null || typeof extensions !== 'object' || Array.isArray(extensions)) {
      return false;
    }
    const code: unknown = (extensions as Record<string, unknown>)['code'];
    return code === 'AUTH_NOT_AUTHENTICATED';
  });
  if (hasAuthFailure) {
    tokens.clearSession();
  }
}
