import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { SKIP_AUTH } from './skip-auth';
import { TokenStorage } from './token-storage';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;
  let tokens: TokenStorage;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
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

  it('attaches Bearer token when stored', () => {
    tokens.setAccessToken('test-jwt');
    http.get('/graphql').subscribe();
    const req = httpTesting.expectOne('/graphql');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-jwt');
    req.flush({});
  });

  it('omits Authorization when no token is stored', () => {
    http.get('/graphql').subscribe();
    const req = httpTesting.expectOne('/graphql');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('skips Authorization when SKIP_AUTH is set', () => {
    tokens.setAccessToken('test-jwt');
    http
      .get('/graphql', { context: new HttpContext().set(SKIP_AUTH, true) })
      .subscribe();
    const req = httpTesting.expectOne('/graphql');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });
});
