import { afterEach, describe, expect, it, vi } from 'vitest';
import * as http from '../shared/http';
import { GraphqlHttpError } from '../shared/graphql.models';
import { LOGIN_MESSAGES } from './auth.messages';
import { login } from './login-api';
import { tokenStorage } from './token-storage';

describe('login', () => {
  afterEach((): void => {
    tokenStorage.clearSession();
    vi.restoreAllMocks();
  });

  it('persists session on success', async (): Promise<void> => {
    vi.spyOn(http, 'graphqlRequest').mockResolvedValue({
      data: {
        login: {
          accessToken: 'jwt-token',
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
    expect(tokenStorage.getAccessToken()).toBe('jwt-token');
    expect(tokenStorage.getTenantSlug()).toBe('acme');
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
          accessToken: 'jwt-token',
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
    tokenStorage.setSession('existing-jwt', 'acme');
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
    expect(tokenStorage.getAccessToken()).toBe('existing-jwt');
  });
});
