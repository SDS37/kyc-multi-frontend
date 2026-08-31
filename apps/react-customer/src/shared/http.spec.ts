import { afterEach, describe, expect, it, vi } from 'vitest';
import { tokenStorage } from '../auth/token-storage';
import { graphqlRequest } from './http';

describe('graphqlRequest', () => {
  afterEach((): void => {
    tokenStorage.clearSession();
    vi.unstubAllGlobals();
  });

  it('attaches Bearer when a token exists', async (): Promise<void> => {
    tokenStorage.setAccessToken('secret-jwt');
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ data: { apiStatus: 'ok' } }),
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
      json: async () => ({ data: { login: null } }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await graphqlRequest('mutation { login }', undefined, { skipAuth: true });

    const init: RequestInit = fetchMock.mock.calls[0]?.[1] as RequestInit;
    const headers: Headers = new Headers(init.headers);
    expect(headers.get('Authorization')).toBeNull();
  });
});
