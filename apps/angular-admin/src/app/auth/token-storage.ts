import { Injectable } from '@angular/core';

const ACCESS_TOKEN_KEY: string = 'kyc.angular-admin.accessToken';
const TENANT_SLUG_KEY: string = 'kyc.angular-admin.tenantSlug';

/**
 * Session storage for the admin app (KYC-060 / KYC-064).
 * Login writes access token + tenant slug; interceptor reads the token.
 * MVP uses sessionStorage so a tab close clears the session.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  getAccessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getTenantSlug(): string | null {
    return sessionStorage.getItem(TENANT_SLUG_KEY);
  }

  setAccessToken(token: string): void {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, token);
  }

  /** Persist JWT + login tenant slug for shell display (slug is not in the JWT). */
  setSession(accessToken: string, tenantSlug: string): void {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    sessionStorage.setItem(TENANT_SLUG_KEY, tenantSlug);
  }

  /** Clear JWT and tenant slug (full sign-out). */
  clearSession(): void {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(TENANT_SLUG_KEY);
  }

  /** @deprecated Prefer `clearSession()` — also clears tenant slug. */
  clearAccessToken(): void {
    this.clearSession();
  }
}
