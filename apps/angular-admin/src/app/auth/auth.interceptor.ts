import {
  HttpEvent,
  HttpHandlerFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG, AppConfig } from '../config/app-config';
import { SKIP_AUTH } from './skip-auth';
import { TokenStorage } from './token-storage';

/**
 * Attaches `Authorization: Bearer <token>` for requests to the configured API origin only.
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

  const token: string | null = inject(TokenStorage).getAccessToken();
  if (!token) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
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
