import { afterEach, describe, expect, it } from 'vitest';
import { appRouter } from './app-router';
import { tokenStorage } from './auth/token-storage';

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

describe('appRouter guards', () => {
  afterEach(async (): Promise<void> => {
    tokenStorage.clearSession();
    await appRouter.replace('/login');
  });

  it('sends guests from /reports to /login', async (): Promise<void> => {
    await appRouter.push('/reports');
    expect(appRouter.currentRoute.value.path).toBe('/login');
    expect(appRouter.currentRoute.value.query['returnUrl']).toBe('/reports');
  });

  it('rejects a Customer session on /reports', async (): Promise<void> => {
    tokenStorage.setSession(
      testJwt({
        sub: '11111111-1111-1111-1111-111111111111',
        tenant_id: '22222222-2222-2222-2222-222222222222',
        role: 'Customer',
        email: 'c@acme.example',
      }),
      'acme',
    );

    await appRouter.push('/reports');
    expect(appRouter.currentRoute.value.path).toBe('/login');
    expect(tokenStorage.getAccessToken()).toBeNull();
  });

  it('lets a TenantAdmin reach /reports', async (): Promise<void> => {
    tokenStorage.setSession(
      testJwt({
        sub: '11111111-1111-1111-1111-111111111111',
        tenant_id: '22222222-2222-2222-2222-222222222222',
        role: 'TenantAdmin',
        email: 'admin@acme.example',
      }),
      'acme',
    );

    await appRouter.push('/reports');
    expect(appRouter.currentRoute.value.path).toBe('/reports');
  });
});
