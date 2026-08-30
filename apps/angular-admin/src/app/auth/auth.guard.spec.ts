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

  it('allows navigation when a token is present', (): void => {
    tokens.setAccessToken('jwt');
    const result: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(
      (): MaybeAsync<GuardResult> => authGuard(routeSnapshot(), stateSnapshot('/cases')),
    );
    expect(result).toBe(true);
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
    tokens.setAccessToken('jwt');
    const result: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(
      (): MaybeAsync<GuardResult> => guestGuard(routeSnapshot(), stateSnapshot('/login')),
    );
    expect(result).toEqual(router.createUrlTree(['/cases']));
  });
});
