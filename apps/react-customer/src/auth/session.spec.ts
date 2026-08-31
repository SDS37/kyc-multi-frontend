import { afterEach, describe, expect, it } from 'vitest';
import { getValidShellSession } from './session';
import { tokenStorage } from './token-storage';

describe('getValidShellSession', () => {
  afterEach((): void => {
    tokenStorage.clearSession();
  });

  it('returns null and clears storage when the JWT is corrupt', (): void => {
    tokenStorage.setSession('not-a-jwt', 'acme');
    expect(getValidShellSession()).toBeNull();
    expect(tokenStorage.getAccessToken()).toBeNull();
    expect(tokenStorage.getTenantSlug()).toBeNull();
  });

  it('returns a session for a well-formed token', (): void => {
    const payload: string = btoa(
      JSON.stringify({
        sub: '11111111-1111-1111-1111-111111111111',
        tenant_id: '22222222-2222-2222-2222-222222222222',
        role: 'Customer',
        email: 'user@acme.example',
      }),
    )
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
    tokenStorage.setSession(`hdr.${payload}.sig`, 'acme');

    const session = getValidShellSession();
    expect(session?.email).toBe('user@acme.example');
    expect(session?.tenantSlug).toBe('acme');
    expect(tokenStorage.getAccessToken()).not.toBeNull();
  });
});
