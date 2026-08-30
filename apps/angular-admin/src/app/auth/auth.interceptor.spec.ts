import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from '../config/app-config';
import { authInterceptor } from './auth.interceptor';
import { SKIP_AUTH } from './skip-auth';
import { TokenStorage } from './token-storage';

const apiBaseUrl = 'http://localhost:5295';
const graphqlUrl = `${apiBaseUrl}/graphql`;

describe('authInterceptor', () => {
  let http: HttpClient;
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
    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenStorage);
    tokens.clearAccessToken();
  });

  afterEach(() => {
    httpTesting.verify();
    tokens.clearAccessToken();
  });

  it('attaches Bearer token for configured API URLs', () => {
    tokens.setAccessToken('test-jwt');
    http.get(graphqlUrl).subscribe();
    const req = httpTesting.expectOne(graphqlUrl);
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-jwt');
    req.flush({});
  });

  it('omits Authorization when no token is stored', () => {
    http.get(graphqlUrl).subscribe();
    const req = httpTesting.expectOne(graphqlUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('skips Authorization when SKIP_AUTH is set', () => {
    tokens.setAccessToken('test-jwt');
    http.get(graphqlUrl, { context: new HttpContext().set(SKIP_AUTH, true) }).subscribe();
    const req = httpTesting.expectOne(graphqlUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('does not attach Authorization for non-API origins', () => {
    tokens.setAccessToken('test-jwt');
    http.get('https://example.com/data').subscribe();
    const req = httpTesting.expectOne('https://example.com/data');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });
});
