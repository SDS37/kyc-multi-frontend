import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  GuardResult,
  MaybeAsync,
  provideRouter,
  Router,
  RouterStateSnapshot,
} from '@angular/router';
import { authGuard, guestGuard } from './auth.guard';
import { TokenStorage } from './token-storage';

function routeSnapshot(): ActivatedRouteSnapshot {
  return {} as ActivatedRouteSnapshot;
}

function stateSnapshot(url: string): RouterStateSnapshot {
  return { url } as RouterStateSnapshot;
}

/** Minimal parseable JWT for shell/guard tests (not cryptographically valid). */
function testAccessToken(): string {
  const header: string = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '');
  const payload: string = btoa(
    JSON.stringify({
      sub: '00000000-0000-0000-0000-000000000001',
      tenant_id: '00000000-0000-0000-0000-000000000002',
      role: 'TenantAdmin',
      email: 'admin@acme.example',
    }),
  )
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '');
  return `${header}.${payload}.sig`;
}

describe('authGuard', () => {
  let tokens: TokenStorage;
  let router: Router;

  beforeEach((): void => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
    tokens = TestBed.inject(TokenStorage);
    router = TestBed.inject(Router);
    tokens.clearSession();
  });

  afterEach((): void => {
    tokens.clearSession();
  });

  it('allows navigation when a parseable token is present', (): void => {
    tokens.setAccessToken(testAccessToken());
    const result: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(
      (): MaybeAsync<GuardResult> => authGuard(routeSnapshot(), stateSnapshot('/cases')),
    );
    expect(result).toBe(true);
  });

  it('rejects corrupt tokens and clears the session', (): void => {
    tokens.setAccessToken('not-a-jwt');
    const result: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(
      (): MaybeAsync<GuardResult> => authGuard(routeSnapshot(), stateSnapshot('/cases')),
    );
    expect(tokens.getAccessToken()).toBeNull();
    expect(result).toEqual(
      router.createUrlTree(['/login'], { queryParams: { returnUrl: '/cases' } }),
    );
  });

  it('redirects to login with returnUrl when no token is stored', (): void => {
    const result: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(
      (): MaybeAsync<GuardResult> => authGuard(routeSnapshot(), stateSnapshot('/cases')),
    );
    expect(result).toEqual(
      router.createUrlTree(['/login'], { queryParams: { returnUrl: '/cases' } }),
    );
  });
});

describe('guestGuard', () => {
  let tokens: TokenStorage;
  let router: Router;

  beforeEach((): void => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
    tokens = TestBed.inject(TokenStorage);
    router = TestBed.inject(Router);
    tokens.clearSession();
  });

  afterEach((): void => {
    tokens.clearSession();
  });

  it('allows login when unauthenticated', (): void => {
    const result: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(
      (): MaybeAsync<GuardResult> => guestGuard(routeSnapshot(), stateSnapshot('/login')),
    );
    expect(result).toBe(true);
  });

  it('sends authenticated users to /cases', (): void => {
    tokens.setAccessToken(testAccessToken());
    const result: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(
      (): MaybeAsync<GuardResult> => guestGuard(routeSnapshot(), stateSnapshot('/login')),
    );
    expect(result).toEqual(router.createUrlTree(['/cases']));
  });
});
