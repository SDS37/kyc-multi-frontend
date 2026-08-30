import { HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { SKIP_AUTH } from './skip-auth';
import { TokenStorage } from './token-storage';

/**
 * Attaches `Authorization: Bearer <token>` when a token is stored.
 * Does not send tenant id headers (ADR-007). Skip with `SKIP_AUTH` for anonymous calls.
 * @see https://angular.dev/guide/http/interceptors
 */
export function authInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  if (req.context.get(SKIP_AUTH)) {
    return next(req);
  }

  const token = inject(TokenStorage).getAccessToken();
  if (!token) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
}
