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

  it('maps transport failures to CasesLoadError NETWORK', (): void => {
    tokens.setAccessToken('jwt-token');
    let error: unknown;

    service.list().subscribe({
      error: (err: unknown): void => {
        error = err;
      },
    });

    httpTesting.expectOne(graphqlUrl).error(new ProgressEvent('error'));

    expect(error).toBeInstanceOf(CasesLoadError);
    expect((error as CasesLoadError).code).toBe('NETWORK');
    expect((error as CasesLoadError).message).toContain('Unable to reach the cases service');
  });

  it('loads case detail by id', (): void => {
    tokens.setAccessToken('jwt-token');
    const caseId: string = '11111111-1111-1111-1111-111111111111';
    let detailId: string | undefined;

    service.getById(caseId).subscribe({
      next: (detail): void => {
        detailId = detail.id;
      },
    });

    const req: TestRequest = httpTesting.expectOne(graphqlUrl);
    expect(req.request.body.variables).toEqual({ id: caseId });
    req.flush({
      data: {
        case: {
          case: {
            id: caseId,
            title: 'Onboarding',
            status: 'IN_REVIEW',
            formData: '{}',
            customerUserId: '22222222-2222-2222-2222-222222222222',
            customerEmail: 'a@b.c',
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-02T00:00:00Z',
            submittedAt: null,
            reviewedAt: null,
            reviewedBy: null,
            reviewComment: null,
          },
          comments: [],
          documents: [],
        },
      },
    });

    expect(detailId).toBe(caseId);
  });

  it('starts review and downloads documents with Bearer token', (): void => {
    tokens.setAccessToken('jwt-token');
    const caseId: string = '11111111-1111-1111-1111-111111111111';
    const docId: string = '33333333-3333-3333-3333-333333333333';
    let status: string | undefined;
    let blobSize: number | undefined;

    service.startReview(caseId).subscribe({
      next: (nextStatus): void => {
        status = nextStatus;
      },
    });
    const startReq: TestRequest = httpTesting.expectOne(graphqlUrl);
    startReq.flush({
      data: { startCaseReview: { id: caseId, status: 'IN_REVIEW', updatedAt: '2026-01-03T00:00:00Z' } },
    });
    expect(status).toBe('IN_REVIEW');

    service.downloadDocument(caseId, docId).subscribe({
      next: (blob: Blob): void => {
        blobSize = blob.size;
      },
    });
    const downloadReq: TestRequest = httpTesting.expectOne(
      `${apiBaseUrl}/api/cases/${caseId}/documents/${docId}`,
    );
    expect(downloadReq.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    downloadReq.flush(new Blob(['pdf']));
    expect(blobSize).toBe(3);
  });
});
