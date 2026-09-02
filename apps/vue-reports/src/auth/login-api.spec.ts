import { afterEach, describe, expect, it, vi } from 'vitest';
import * as http from '../shared/http';
import { GraphqlHttpError } from '../shared/graphql.models';
import { LOGIN_MESSAGES } from './auth.messages';
import { login } from './login-api';
import { tokenStorage } from './token-storage';

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

const adminJwt: string = testJwt({
  sub: '11111111-1111-1111-1111-111111111111',
  tenant_id: '22222222-2222-2222-2222-222222222222',
  role: 'TenantAdmin',
  email: 'admin@acme.example',
});

const customerJwt: string = testJwt({
  sub: '11111111-1111-1111-1111-111111111111',
  tenant_id: '22222222-2222-2222-2222-222222222222',
  role: 'Customer',
  email: 'customer1@acme.example',
});

describe('login', () => {
  afterEach((): void => {
    tokenStorage.clearSession();
    vi.restoreAllMocks();
  });

  it('persists session on success for TenantAdmin', async (): Promise<void> => {
    vi.spyOn(http, 'graphqlRequest').mockResolvedValue({
      data: {
        login: {
          accessToken: adminJwt,
          tokenType: 'Bearer',
          expiresInSeconds: 3600,
        },
      },
    });

    await login({
      tenantSlug: ' acme ',
      email: ' admin@acme.example ',
      password: 'ChangeMe1',
    });

    expect(http.graphqlRequest).toHaveBeenCalledWith(
      expect.stringContaining('mutation Login'),
      {
        input: {
          tenantSlug: 'acme',
          email: 'admin@acme.example',
          password: 'ChangeMe1',
        },
      },
      { skipAuth: true },
    );
    expect(tokenStorage.getAccessToken()).toBe(adminJwt);
    expect(tokenStorage.getTenantSlug()).toBe('acme');
  });

  it('rejects Customer roles without persisting a session', async (): Promise<void> => {
    vi.spyOn(http, 'graphqlRequest').mockResolvedValue({
      data: {
        login: {
          accessToken: customerJwt,
          tokenType: 'Bearer',
          expiresInSeconds: 3600,
        },
      },
    });

    await expect(
      login({
        tenantSlug: 'acme',
        email: 'customer1@acme.example',
        password: 'ChangeMe1',
      }),
    ).rejects.toMatchObject({
      name: 'LoginFailedError',
      message: LOGIN_MESSAGES.wrongAppRole,
    });
    expect(tokenStorage.getAccessToken()).toBeNull();
  });

  it('maps GraphQL AUTH_FAILED to LoginFailedError', async (): Promise<void> => {
    vi.spyOn(http, 'graphqlRequest').mockResolvedValue({
      errors: [{ message: 'Invalid credentials.', extensions: { code: 'AUTH_FAILED' } }],
    });

    await expect(
      login({
        tenantSlug: 'acme',
        email: 'admin@acme.example',
        password: 'wrong',
      }),
    ).rejects.toMatchObject({
      name: 'LoginFailedError',
      message: 'Invalid credentials.',
      code: 'AUTH_FAILED',
    });
    expect(tokenStorage.getAccessToken()).toBeNull();
  });

  it('omits captchaToken when absent and includes it when present', async (): Promise<void> => {
    vi.spyOn(http, 'graphqlRequest').mockResolvedValue({
      data: {
        login: {
          accessToken: adminJwt,
          tokenType: 'Bearer',
          expiresInSeconds: 3600,
        },
      },
    });

    await login({
      tenantSlug: 'acme',
      email: 'admin@acme.example',
      password: 'ChangeMe1',
    });
    expect(http.graphqlRequest).toHaveBeenCalledWith(
      expect.stringContaining('mutation Login'),
      {
        input: {
          tenantSlug: 'acme',
          email: 'admin@acme.example',
          password: 'ChangeMe1',
        },
      },
      { skipAuth: true },
    );

    await login({
      tenantSlug: 'acme',
      email: 'admin@acme.example',
      password: 'ChangeMe1',
      captchaToken: ' turnstile-token ',
    });
    expect(http.graphqlRequest).toHaveBeenLastCalledWith(
      expect.stringContaining('mutation Login'),
      {
        input: {
          tenantSlug: 'acme',
          email: 'admin@acme.example',
          password: 'ChangeMe1',
          captchaToken: 'turnstile-token',
        },
      },
      { skipAuth: true },
    );
  });

  it('maps HTTP 429 to a dedicated rate-limit error without storing a token', async (): Promise<void> => {
    tokenStorage.setSession(adminJwt, 'acme');
    vi.spyOn(http, 'graphqlRequest').mockRejectedValue(new GraphqlHttpError(429));

    await expect(
      login({
        tenantSlug: 'acme',
        email: 'admin@acme.example',
        password: 'ChangeMe1',
      }),
    ).rejects.toMatchObject({
      name: 'LoginFailedError',
      message: LOGIN_MESSAGES.rateLimited,
      code: 'RATE_LIMITED',
    });
    expect(tokenStorage.getAccessToken()).toBe(adminJwt);
  });
});
