import { afterEach, describe, expect, it } from 'vitest';
import { tokenStorage } from './token-storage';

describe('tokenStorage', () => {
  afterEach((): void => {
    tokenStorage.clearSession();
  });

  it('stores and clears a session', (): void => {
    tokenStorage.setSession('jwt-token', 'acme');
    expect(tokenStorage.getAccessToken()).toBe('jwt-token');
    expect(tokenStorage.getTenantSlug()).toBe('acme');
    tokenStorage.clearSession();
    expect(tokenStorage.getAccessToken()).toBeNull();
    expect(tokenStorage.getTenantSlug()).toBeNull();
  });
});
