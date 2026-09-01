import { afterEach, describe, expect, it, vi } from 'vitest';
import { tokenStorage } from '../auth/token-storage';
import { apiFetch, graphqlRequest } from './http';

describe('graphqlRequest', () => {
  afterEach((): void => {
    tokenStorage.clearSession();
    vi.unstubAllGlobals();
  });

  it('attaches Bearer when a token exists', async (): Promise<void> => {
    tokenStorage.setAccessToken('secret-jwt');
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<{ data: { apiStatus: string } }> => ({
        data: { apiStatus: 'ok' },
      }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await graphqlRequest<{ apiStatus: string }>('{ apiStatus }');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const init: RequestInit = fetchMock.mock.calls[0]?.[1] as RequestInit;
    const headers: Headers = new Headers(init.headers);
    expect(headers.get('Authorization')).toBe('Bearer secret-jwt');
  });

  it('skips Authorization when skipAuth is set', async (): Promise<void> => {
    tokenStorage.setAccessToken('secret-jwt');
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<{ data: { login: null } }> => ({ data: { login: null } }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await graphqlRequest('mutation { login }', undefined, { skipAuth: true });

    const init: RequestInit = fetchMock.mock.calls[0]?.[1] as RequestInit;
    const headers: Headers = new Headers(init.headers);
    expect(headers.get('Authorization')).toBeNull();
  });

  it('clears the session on HTTP 401', async (): Promise<void> => {
    tokenStorage.setSession('secret-jwt', 'acme');
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async (): Promise<Record<string, never>> => ({}),
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(graphqlRequest('{ apiStatus }')).rejects.toThrow(/401/);
    expect(tokenStorage.getAccessToken()).toBeNull();
    expect(tokenStorage.getTenantSlug()).toBeNull();
  });
});

describe('apiFetch', () => {
  afterEach((): void => {
    tokenStorage.clearSession();
    vi.unstubAllGlobals();
  });

  it('clears the session on HTTP 401', async (): Promise<void> => {
    tokenStorage.setSession('secret-jwt', 'acme');
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
    });
    vi.stubGlobal('fetch', fetchMock);

    const response: Response = await apiFetch('/api/cases');
    expect(response.status).toBe(401);
    expect(tokenStorage.getAccessToken()).toBeNull();
  });
});
