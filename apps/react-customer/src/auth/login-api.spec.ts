import { afterEach, describe, expect, it, vi } from 'vitest';
import * as http from '../shared/http';
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
});
