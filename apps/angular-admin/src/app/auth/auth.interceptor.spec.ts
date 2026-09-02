import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_CONFIG, AppConfig } from '../config/app-config';
import { authInterceptor } from './auth.interceptor';
import { SKIP_AUTH } from './skip-auth';
import { TokenStorage } from './token-storage';

const apiBaseUrl: string = 'http://localhost:5295';
const graphqlUrl: string = `${apiBaseUrl}/graphql`;
const testAppConfig: AppConfig = {
  apiBaseUrl,
  graphqlUrl,
  captchaRequiredForLogin: false,
  turnstileSiteKey: '',
};

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;
  let tokens: TokenStorage;

  beforeEach((): void => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
        {
          provide: APP_CONFIG,
          useValue: testAppConfig,
        },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenStorage);
    tokens.clearSession();
  });

  afterEach((): void => {
    httpTesting.verify();
    tokens.clearSession();
  });

  it('attaches Bearer token for configured API URLs', (): void => {
    tokens.setAccessToken('test-jwt');
    http.get(graphqlUrl).subscribe();
    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-jwt');
    req.flush({});
  });

  it('omits Authorization when no token is stored', (): void => {
    http.get(graphqlUrl).subscribe();
    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('skips Authorization when SKIP_AUTH is set', (): void => {
    tokens.setAccessToken('test-jwt');
    http
      .get(graphqlUrl, { context: new HttpContext().set(SKIP_AUTH, true) })
      .subscribe();
    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('does not attach Authorization for non-API origins', (): void => {
    tokens.setAccessToken('test-jwt');
    http.get('https://example.com/data').subscribe();
    const req: TestRequest = httpTesting.expectOne('https://example.com/data');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('clears the session on HTTP 401', (): void => {
    tokens.setAccessToken('test-jwt');
    http.get(graphqlUrl).subscribe({
      error: (): void => undefined,
    });
    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    req.flush('unauthorized', { status: 401, statusText: 'Unauthorized' });
    expect(tokens.getAccessToken()).toBeNull();
  });

  it('clears the session on GraphQL AUTH_NOT_AUTHENTICATED', (): void => {
    tokens.setAccessToken('test-jwt');
    http.post(graphqlUrl, { query: '{ me { id } }' }).subscribe();
    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    req.flush({
      data: null,
      errors: [{ message: 'The current user is not authorized to access this resource.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } }],
    });
    expect(tokens.getAccessToken()).toBeNull();
  });

  it('keeps the session on unrelated GraphQL errors', (): void => {
    tokens.setAccessToken('test-jwt');
    http.post(graphqlUrl, { query: '{ cases { items { id } } }' }).subscribe();
    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    req.flush({
      data: null,
      errors: [{ message: 'boom', extensions: { code: 'VALIDATION' } }],
    });
    expect(tokens.getAccessToken()).toBe('test-jwt');
  });

  it('keeps the session on HTTP 429', (): void => {
    tokens.setAccessToken('test-jwt');
    http.post(graphqlUrl, { query: '{ apiStatus }' }).subscribe({
      error: (): void => undefined,
    });
    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    req.flush({ error: 'Too many requests.' }, { status: 429, statusText: 'Too Many Requests' });
    expect(tokens.getAccessToken()).toBe('test-jwt');
  });
});
