import { describe, expect, it } from 'vitest';
import {
  normalizeLoginCredentials,
  parseAccessTokenClaims,
  parseLoginSuccess,
  resolvePostLoginUrl,
  toLoginFailedError,
  toShellSession,
  validateLoginForm,
} from './auth.mappers';
import { LoginFailedError } from './auth.models';

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
    expect(resolvePostLoginUrl('/cases?x=1')).toBe('/cases?x=1');
    expect(resolvePostLoginUrl('//evil.example')).toBe('/cases');
    expect(resolvePostLoginUrl('https://evil.example')).toBe('/cases');
    expect(resolvePostLoginUrl(null)).toBe('/cases');
  });

  it('toLoginFailedError maps network failures', (): void => {
    const mapped: LoginFailedError = toLoginFailedError(new TypeError('Failed to fetch'));
    expect(mapped.code).toBe('NETWORK');
  });

  it('parseAccessTokenClaims reads email role and tenant_id', (): void => {
    const payload: string = btoa(
      JSON.stringify({
        sub: '11111111-1111-1111-1111-111111111111',
        tenant_id: '22222222-2222-2222-2222-222222222222',
        role: 'TenantAdmin',
        email: 'admin@acme.example',
      }),
    )
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
    const token: string = `hdr.${payload}.sig`;

    expect(parseAccessTokenClaims(token)).toEqual({
      subject: '11111111-1111-1111-1111-111111111111',
      tenantId: '22222222-2222-2222-2222-222222222222',
      role: 'TenantAdmin',
      email: 'admin@acme.example',
    });
  });

  it('toShellSession prefers tenant slug when present', (): void => {
    const payload: string = btoa(
      JSON.stringify({
        sub: '11111111-1111-1111-1111-111111111111',
        tenant_id: '22222222-2222-2222-2222-222222222222',
        role: 'Reviewer',
        email: 'rev@acme.example',
      }),
    )
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
    const token: string = `hdr.${payload}.sig`;

    expect(toShellSession(token, 'acme')?.tenantSlug).toBe('acme');
    expect(toShellSession(token, null)?.tenantSlug).toBeNull();
    expect(toShellSession(null, 'acme')).toBeNull();
  });
});
