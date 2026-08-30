import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from '../config/app-config';
import { authInterceptor } from './auth.interceptor';
import { LoginFailedError, LoginService } from './login.service';
import { TokenStorage } from './token-storage';

const apiBaseUrl = 'http://localhost:5295';
const graphqlUrl = `${apiBaseUrl}/graphql`;

describe('LoginService', () => {
  let service: LoginService;
  let httpTesting: HttpTestingController;
  let tokens: TokenStorage;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        {
          provide: APP_CONFIG,
          useValue: { apiBaseUrl, graphqlUrl },
        },
      ],
    });
    service = TestBed.inject(LoginService);
    httpTesting = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenStorage);
    tokens.clearAccessToken();
  });

  afterEach(() => {
    httpTesting.verify();
    tokens.clearAccessToken();
  });

  it('posts GraphQL login without Authorization and stores the access token', () => {
    let succeeded = false;
    service
      .login({
        tenantSlug: ' Acme ',
        email: ' reviewer@acme.test ',
        password: 'secret',
      })
      .subscribe({
        next: (result) => {
          expect(result.accessToken).toBe('jwt-token');
          succeeded = true;
        },
      });

    const req = httpTesting.expectOne(graphqlUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(req.request.body.variables.input).toEqual({
      tenantSlug: 'Acme',
      email: 'reviewer@acme.test',
      password: 'secret',
    });

    req.flush({
      data: {
        login: {
          accessToken: 'jwt-token',
          tokenType: 'Bearer',
          expiresInSeconds: 3600,
        },
      },
    });

    expect(succeeded).toBe(true);
    expect(tokens.getAccessToken()).toBe('jwt-token');
  });

  it('maps GraphQL AUTH_FAILED to LoginFailedError without storing a token', () => {
    let error: unknown;
    service
      .login({ tenantSlug: 'acme', email: 'a@b.c', password: 'bad' })
      .subscribe({
        error: (err) => {
          error = err;
        },
      });

    httpTesting.expectOne(graphqlUrl).flush({
      errors: [{ message: 'Invalid email, password, or tenant.', extensions: { code: 'AUTH_FAILED' } }],
    });

    expect(error).toBeInstanceOf(LoginFailedError);
    expect((error as LoginFailedError).code).toBe('AUTH_FAILED');
    expect(tokens.getAccessToken()).toBeNull();
  });

  it('maps HTTP failures to a network LoginFailedError', () => {
    let error: unknown;
    service
      .login({ tenantSlug: 'acme', email: 'a@b.c', password: 'x' })
      .subscribe({
        error: (err) => {
          error = err;
        },
      });

    httpTesting.expectOne(graphqlUrl).error(new ProgressEvent('error'), { status: 0 });

    expect(error).toBeInstanceOf(LoginFailedError);
    expect((error as LoginFailedError).code).toBe('NETWORK');
  });
});
