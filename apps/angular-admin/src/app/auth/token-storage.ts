import { Injectable } from '@angular/core';

const ACCESS_TOKEN_KEY: string = 'kyc.angular-admin.accessToken';

/**
 * JWT access-token storage for the admin app (KYC-060).
 * Login (KYC-061) writes here; the auth interceptor reads from here.
 * MVP uses sessionStorage so a tab close clears the session.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  getAccessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  }

  setAccessToken(token: string): void {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, token);
  }

  clearAccessToken(): void {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
  }
}
