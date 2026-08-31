const ACCESS_TOKEN_KEY: string = 'kyc.react-customer.accessToken';
const TENANT_SLUG_KEY: string = 'kyc.react-customer.tenantSlug';

/**
 * Session storage for the customer app (KYC-070).
 * Login (KYC-071) writes access token + tenant slug; HTTP helper reads the token.
 * MVP uses sessionStorage so a tab close clears the session.
 */
export const tokenStorage = {
  getAccessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  },

  getTenantSlug(): string | null {
    return sessionStorage.getItem(TENANT_SLUG_KEY);
  },

  setAccessToken(token: string): void {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, token);
  },

  /** Persist JWT + login tenant slug for shell display (slug is not in the JWT). */
  setSession(accessToken: string, tenantSlug: string): void {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    sessionStorage.setItem(TENANT_SLUG_KEY, tenantSlug);
  },

  /** Clear JWT and tenant slug (full sign-out). */
  clearSession(): void {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(TENANT_SLUG_KEY);
  },
} as const;
