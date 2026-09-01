import { describe, expect, it } from 'vitest';
import {
  normalizeLoginCredentials,
  parseAccessTokenClaims,
  parseLoginSuccess,
  resolvePostLoginUrl,
  resolveReportsNavigation,
  toLoginFailedError,
  toShellSession,
  validateLoginForm,
} from './auth.mappers';
import { LoginFailedError } from './auth.models';

function testJwt(claims: Record<string, unknown>): string {
  const payload: string = btoa(
    JSON.stringify({
      ...claims,
      exp: Math.floor(Date.now() / 1000) + 3600,
    }),
  )
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
  return `hdr.${payload}.sig`;
}

describe('auth.mappers', () => {
  it('normalizeLoginCredentials trims slug and email only', (): void => {
    expect(
      normalizeLoginCredentials({
        tenantSlug: ' Acme ',
        email: ' a@b.c ',
        password: '  keep  ',
      }),
    ).toEqual({
      tenantSlug: 'Acme',
      email: 'a@b.c',
      password: '  keep  ',
    });
  });

  it('validateLoginForm reports required fields', (): void => {
    const errors = validateLoginForm({
      tenantSlug: '',
      email: '',
      password: '',
    });
    expect(errors.tenantSlug).toBeDefined();
    expect(errors.email).toBeDefined();
    expect(errors.password).toBeDefined();
  });

  it('parseLoginSuccess returns the login payload', (): void => {
    expect(
      parseLoginSuccess({
        data: {
          login: {
            accessToken: 'jwt',
            tokenType: 'Bearer',
            expiresInSeconds: 60,
          },
        },
      }),
    ).toEqual({
      accessToken: 'jwt',
      tokenType: 'Bearer',
      expiresInSeconds: 60,
    });
  });

  it('parseLoginSuccess throws LoginFailedError on GraphQL errors', (): void => {
    expect(() =>
      parseLoginSuccess({
        errors: [{ message: 'Invalid.', extensions: { code: 'AUTH_FAILED' } }],
      }),
    ).toThrow(LoginFailedError);
  });

  it('resolvePostLoginUrl blocks open redirects', (): void => {
    expect(resolvePostLoginUrl('/reports?x=1')).toBe('/reports?x=1');
    expect(resolvePostLoginUrl('//evil.example')).toBe('/reports');
    expect(resolvePostLoginUrl('https://evil.example')).toBe('/reports');
    expect(resolvePostLoginUrl(null)).toBe('/reports');
  });

  it('toLoginFailedError maps network failures', (): void => {
    const mapped: LoginFailedError = toLoginFailedError(new TypeError('Failed to fetch'));
    expect(mapped.code).toBe('NETWORK');
  });

  it('parseAccessTokenClaims reads email role and tenant_id', (): void => {
    const token: string = testJwt({
      sub: '11111111-1111-1111-1111-111111111111',
      tenant_id: '22222222-2222-2222-2222-222222222222',
      role: 'TenantAdmin',
      email: 'admin@acme.example',
    });

    expect(parseAccessTokenClaims(token)).toEqual({
      subject: '11111111-1111-1111-1111-111111111111',
      tenantId: '22222222-2222-2222-2222-222222222222',
      role: 'TenantAdmin',
      email: 'admin@acme.example',
    });
  });

  it('toShellSession prefers tenant slug when present', (): void => {
    const token: string = testJwt({
      sub: '11111111-1111-1111-1111-111111111111',
      tenant_id: '22222222-2222-2222-2222-222222222222',
      role: 'Reviewer',
      email: 'rev@acme.example',
    });

    expect(toShellSession(token, 'acme')?.tenantSlug).toBe('acme');
    expect(toShellSession(token, null)?.tenantSlug).toBeNull();
    expect(toShellSession(null, 'acme')).toBeNull();
  });

  it('resolveReportsNavigation sends guests to login with a safe returnUrl', (): void => {
    const redirect = resolveReportsNavigation(
      { fullPath: '/reports', meta: { requiresAuth: true } },
      null,
    );
    expect(redirect).toEqual({
      path: '/login',
      query: { returnUrl: '/reports' },
      replace: true,
      clearSession: false,
    });
  });

  it('resolveReportsNavigation rejects Customer sessions on protected routes', (): void => {
    const redirect = resolveReportsNavigation(
      { fullPath: '/reports', meta: { requiresAuth: true } },
      {
        tenantSlug: 'acme',
        tenantId: 'tid',
        email: 'c@acme.example',
        role: 'Customer',
      },
    );
    expect(redirect?.path).toBe('/login');
    expect(redirect?.clearSession).toBe(true);
  });

  it('resolveReportsNavigation sends signed-in reviewers away from login', (): void => {
    const redirect = resolveReportsNavigation(
      { fullPath: '/login', meta: { guestOnly: true } },
      {
        tenantSlug: 'acme',
        tenantId: 'tid',
        email: 'r@acme.example',
        role: 'Reviewer',
      },
    );
    expect(redirect).toEqual({
      path: '/reports',
      replace: true,
      clearSession: false,
    });
  });

  it('resolveReportsNavigation keeps Reviewer/TenantAdmin on reports', (): void => {
    expect(
      resolveReportsNavigation(
        { fullPath: '/reports', meta: { requiresAuth: true } },
        {
          tenantSlug: 'acme',
          tenantId: 'tid',
          email: 'r@acme.example',
          role: 'Reviewer',
        },
      ),
    ).toBeNull();
  });
});
