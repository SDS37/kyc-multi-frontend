import { HttpErrorResponse } from '@angular/common/http';
import {
  normalizeLoginCredentials,
  parseAccessTokenClaims,
  parseLoginSuccess,
  resolvePostLoginUrl,
  toLoginFailedError,
  toLoginMutationInput,
  toShellSession,
} from './auth.mappers';
import { LOGIN_MESSAGES } from './auth.messages';
import { LoginFailedError, RATE_LIMITED_CODE } from './auth.models';

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

  it('toLoginMutationInput omits captchaToken when absent and includes it when present', (): void => {
    expect(
      toLoginMutationInput({
        tenantSlug: 'acme',
        email: 'a@b.c',
        password: 'secret',
      }),
    ).toEqual({
      tenantSlug: 'acme',
      email: 'a@b.c',
      password: 'secret',
    });
    expect(
      toLoginMutationInput({
        tenantSlug: 'acme',
        email: 'a@b.c',
        password: 'secret',
        captchaToken: '  token-1  ',
      }),
    ).toEqual({
      tenantSlug: 'acme',
      email: 'a@b.c',
      password: 'secret',
      captchaToken: 'token-1',
    });
    expect(
      Object.prototype.hasOwnProperty.call(
        toLoginMutationInput({
          tenantSlug: 'acme',
          email: 'a@b.c',
          password: 'secret',
          captchaToken: '   ',
        }),
        'captchaToken',
      ),
    ).toBe(false);
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
    const mapped: LoginFailedError = toLoginFailedError(
      new HttpErrorResponse({ status: 0 }),
    );
    expect(mapped.code).toBe('NETWORK');
  });

  it('toLoginFailedError maps HTTP 429 to the rate-limit message', (): void => {
    const mapped: LoginFailedError = toLoginFailedError(
      new HttpErrorResponse({ status: 429, statusText: 'Too Many Requests' }),
    );
    expect(mapped.code).toBe(RATE_LIMITED_CODE);
    expect(mapped.message).toBe(LOGIN_MESSAGES.rateLimited);
  });

  it('toLoginFailedError maps an unreachable API to the network message', (): void => {
    const fetchFailure: LoginFailedError = toLoginFailedError(new TypeError('Failed to fetch'));
    expect(fetchFailure.code).toBe('NETWORK');
    expect(fetchFailure.message).toBe(LOGIN_MESSAGES.networkFailed);

    const networkError: LoginFailedError = toLoginFailedError(
      new Error('NetworkError when attempting to fetch resource'),
    );
    expect(networkError.code).toBe('NETWORK');
    expect(networkError.message).toBe(LOGIN_MESSAGES.networkFailed);
  });

  it('toLoginFailedError keeps an unknown error on the sign-in message', (): void => {
    const mapped: LoginFailedError = toLoginFailedError(new Error('boom'));
    expect(mapped.message).toBe(LOGIN_MESSAGES.signInFailed);
    expect(mapped.code).toBeUndefined();
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

  it('parseAccessTokenClaims rejects expired tokens', (): void => {
    const payload: string = btoa(
      JSON.stringify({
        sub: '11111111-1111-1111-1111-111111111111',
        tenant_id: '22222222-2222-2222-2222-222222222222',
        role: 'TenantAdmin',
        email: 'admin@acme.example',
        exp: Math.floor(Date.now() / 1000) - 10,
      }),
    )
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
    expect(parseAccessTokenClaims(`hdr.${payload}.sig`)).toBeNull();
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
});
