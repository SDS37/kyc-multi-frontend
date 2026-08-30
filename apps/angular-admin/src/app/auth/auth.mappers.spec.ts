import { HttpErrorResponse } from '@angular/common/http';
import {
  normalizeLoginCredentials,
  parseLoginSuccess,
  resolvePostLoginUrl,
  toLoginFailedError,
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
});
