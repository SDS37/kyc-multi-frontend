import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authGuard, guestGuard } from './auth.guard';
import { TokenStorage } from './token-storage';

describe('authGuard', () => {
  let tokens: TokenStorage;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
    tokens = TestBed.inject(TokenStorage);
    router = TestBed.inject(Router);
    tokens.clearAccessToken();
  });

  afterEach(() => {
    tokens.clearAccessToken();
  });

  it('allows navigation when a token is present', () => {
    tokens.setAccessToken('jwt');
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/cases' } as never),
    );
    expect(result).toBe(true);
  });

  it('redirects to login with returnUrl when no token is stored', () => {
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/cases' } as never),
    );
    expect(result).toEqual(
      router.createUrlTree(['/login'], { queryParams: { returnUrl: '/cases' } }),
    );
  });
});

describe('guestGuard', () => {
  let tokens: TokenStorage;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
    tokens = TestBed.inject(TokenStorage);
    router = TestBed.inject(Router);
    tokens.clearAccessToken();
  });

  afterEach(() => {
    tokens.clearAccessToken();
  });

  it('allows login when unauthenticated', () => {
    const result = TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));
    expect(result).toBe(true);
  });

  it('sends authenticated users to /cases', () => {
    tokens.setAccessToken('jwt');
    const result = TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));
    expect(result).toEqual(router.createUrlTree(['/cases']));
  });
});
