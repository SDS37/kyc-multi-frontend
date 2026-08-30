import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from '../config/app-config';
import { authInterceptor } from './auth.interceptor';
import { LoginFailedError, LoginSuccess } from './auth.models';
import { LoginService } from './login.service';
import { TokenStorage } from './token-storage';

const apiBaseUrl: string = 'http://localhost:5295';
const graphqlUrl: string = `${apiBaseUrl}/graphql`;

describe('LoginService', () => {
  let service: LoginService;
  let httpTesting: HttpTestingController;
  let tokens: TokenStorage;

  beforeEach((): void => {
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
    tokens.clearSession();
  });

  afterEach((): void => {
    httpTesting.verify();
    tokens.clearSession();
  });

  it('posts GraphQL login without Authorization and stores the access token', (): void => {
    let succeeded: boolean = false;
    service
      .login({
        tenantSlug: ' Acme ',
        email: ' reviewer@acme.test ',
        password: 'secret',
      })
      .subscribe({
        next: (result: LoginSuccess): void => {
          expect(result.accessToken).toBe('jwt-token');
          succeeded = true;
        },
      });

    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
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
    expect(tokens.getTenantSlug()).toBe('Acme');
  });

  it('maps GraphQL AUTH_FAILED to LoginFailedError without storing a token', (): void => {
    let error: unknown;
    service
      .login({ tenantSlug: 'acme', email: 'a@b.c', password: 'bad' })
      .subscribe({
        error: (err: unknown): void => {
          error = err;
        },
      });

    httpTesting.expectOne(graphqlUrl).flush({
      errors: [
        { message: 'Invalid email, password, or tenant.', extensions: { code: 'AUTH_FAILED' } },
      ],
    });

    expect(error).toBeInstanceOf(LoginFailedError);
    expect((error as LoginFailedError).code).toBe('AUTH_FAILED');
    expect(tokens.getAccessToken()).toBeNull();
  });

  it('maps HTTP failures to a network LoginFailedError', (): void => {
    let error: unknown;
    service
      .login({ tenantSlug: 'acme', email: 'a@b.c', password: 'x' })
      .subscribe({
        error: (err: unknown): void => {
          error = err;
        },
      });

    httpTesting.expectOne(graphqlUrl).error(new ProgressEvent('error'), { status: 0 });

    expect(error).toBeInstanceOf(LoginFailedError);
    expect((error as LoginFailedError).code).toBe('NETWORK');
  });
});
