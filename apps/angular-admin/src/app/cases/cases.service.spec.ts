import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from '../config/app-config';
import { authInterceptor } from '../auth/auth.interceptor';
import { TokenStorage } from '../auth/token-storage';
import { CaseListPage, CasesLoadError } from './cases.models';
import { CasesService } from './cases.service';

const apiBaseUrl: string = 'http://localhost:5295';
const graphqlUrl: string = `${apiBaseUrl}/graphql`;

describe('CasesService', () => {
  let service: CasesService;
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
    service = TestBed.inject(CasesService);
    httpTesting = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenStorage);
    tokens.clearAccessToken();
  });

  afterEach((): void => {
    httpTesting.verify();
    tokens.clearAccessToken();
  });

  it('lists cases with Bearer token and maps items', (): void => {
    tokens.setAccessToken('jwt-token');
    let page: CaseListPage | undefined;

    service.list({ status: 'SUBMITTED', skip: 0, take: 20 }).subscribe({
      next: (result: CaseListPage): void => {
        page = result;
      },
    });

    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    expect(req.request.body.variables).toEqual({
      status: 'SUBMITTED',
      skip: 0,
      take: 20,
    });

    req.flush({
      data: {
        cases: {
          totalCount: 1,
          skip: 0,
          take: 20,
          items: [
            {
              id: '11111111-1111-1111-1111-111111111111',
              title: 'Acme onboarding',
              status: 'SUBMITTED',
              customerEmail: 'customer@acme.example',
              updatedAt: '2026-08-30T12:00:00Z',
            },
          ],
        },
      },
    });

    expect(page).toEqual({
      totalCount: 1,
      skip: 0,
      take: 20,
      items: [
        {
          id: '11111111-1111-1111-1111-111111111111',
          title: 'Acme onboarding',
          status: 'SUBMITTED',
          customerEmail: 'customer@acme.example',
          updatedAt: '2026-08-30T12:00:00Z',
        },
      ],
    });
  });

  it('maps GraphQL errors to CasesLoadError', (): void => {
    tokens.setAccessToken('jwt-token');
    let error: unknown;

    service.list().subscribe({
      error: (err: unknown): void => {
        error = err;
      },
    });

    httpTesting.expectOne(graphqlUrl).flush({
      errors: [{ message: 'Not authorized.', extensions: { code: 'AUTH_FAILED' } }],
    });

    expect(error).toBeInstanceOf(CasesLoadError);
    expect((error as CasesLoadError).code).toBe('AUTH_FAILED');
  });

  it('rejects unknown status values', (): void => {
    tokens.setAccessToken('jwt-token');
    let error: unknown;

    service.list().subscribe({
      error: (err: unknown): void => {
        error = err;
      },
    });

    httpTesting.expectOne(graphqlUrl).flush({
      data: {
        cases: {
          totalCount: 1,
          skip: 0,
          take: 20,
          items: [
            {
              id: '11111111-1111-1111-1111-111111111111',
              title: 'Bad',
              status: 'NOPE',
              customerEmail: 'a@b.c',
              updatedAt: '2026-08-30T12:00:00Z',
            },
          ],
        },
      },
    });

    expect(error).toBeInstanceOf(CasesLoadError);
  });
});
