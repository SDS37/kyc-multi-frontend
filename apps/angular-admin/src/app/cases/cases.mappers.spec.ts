import { HttpErrorResponse } from '@angular/common/http';
import {
  normalizeRejectComment,
  parseCaseDetail,
  parseCaseFormData,
  parseCasesPage,
  parseStatusFilterValue,
  resolveReviewActions,
  toCaseActionError,
  toCaseDownloadError,
  toCasesLoadError,
  toDocumentDownloadUrl,
  toListCasesVariables,
} from './cases.mappers';
import { CaseActionError, CaseDownloadError, CasesLoadError } from './cases.models';
import { CASES_LIST_MESSAGES, CASES_REVIEW_MESSAGES } from './cases.messages';

describe('cases.mappers', () => {
  it('toListCasesVariables applies defaults', (): void => {
    expect(toListCasesVariables({})).toEqual({
      status: null,
      skip: 0,
      take: 20,
    });
  });

  it('parseCasesPage maps items', (): void => {
    const page = parseCasesPage(
      {
        data: {
          cases: {
            totalCount: 1,
            skip: 0,
            take: 20,
            items: [
              {
                id: '1',
                title: 'T',
                status: 'DRAFT',
                customerEmail: 'a@b.c',
                updatedAt: '2026-01-01T00:00:00Z',
              },
            ],
          },
        },
      },
      { status: null, skip: 0, take: 20 },
    );

    expect(page.items).toHaveLength(1);
    expect(page.items[0]?.status).toBe('DRAFT');
  });

  it('parseCasesPage rejects unknown status', (): void => {
    expect(() =>
      parseCasesPage(
        {
          data: {
            cases: {
              totalCount: 1,
              skip: 0,
              take: 20,
              items: [
                {
                  id: '1',
                  title: 'T',
                  status: 'NOPE',
                  customerEmail: 'a@b.c',
                  updatedAt: '2026-01-01T00:00:00Z',
                },
              ],
            },
          },
        },
        { status: null, skip: 0, take: 20 },
      ),
    ).toThrow(CasesLoadError);
  });

  it('parseStatusFilterValue accepts null and known statuses', (): void => {
    expect(parseStatusFilterValue(null)).toBeNull();
    expect(parseStatusFilterValue('IN_REVIEW')).toBe('IN_REVIEW');
    expect(parseStatusFilterValue('nope')).toBeUndefined();
  });

  it('toCasesLoadError maps network failures', (): void => {
    expect(toCasesLoadError(new HttpErrorResponse({ status: 0 })).code).toBe('NETWORK');
  });

  it('toCasesLoadError maps HTTP 429 to a rate-limit message', (): void => {
    const mapped: CasesLoadError = toCasesLoadError(
      new HttpErrorResponse({ status: 429, statusText: 'Too Many Requests' }),
    );
    expect(mapped.code).toBe('RATE_LIMITED');
    expect(mapped.message).toBe(CASES_LIST_MESSAGES.listRateLimited);
  });

  it('toCasesLoadError maps an unreachable API to the network message', (): void => {
    const fetchFailure: CasesLoadError = toCasesLoadError(new TypeError('Failed to fetch'));
    expect(fetchFailure.code).toBe('NETWORK');
    expect(fetchFailure.message).toBe(CASES_LIST_MESSAGES.listNetworkFailed);

    const networkError: CasesLoadError = toCasesLoadError(
      new Error('NetworkError when attempting to fetch resource'),
    );
    expect(networkError.code).toBe('NETWORK');
    expect(networkError.message).toBe(CASES_LIST_MESSAGES.listNetworkFailed);
  });

  it('toCasesLoadError keeps an unknown error on the list-failed message', (): void => {
    const mapped: CasesLoadError = toCasesLoadError(new Error('boom'));
    expect(mapped.message).toBe(CASES_LIST_MESSAGES.listLoadFailed);
    expect(mapped.code).toBeUndefined();
  });

  it('toCaseDownloadError maps an unreachable API to the network message', (): void => {
    const fetchFailure: CaseDownloadError = toCaseDownloadError(new TypeError('Failed to fetch'));
    expect(fetchFailure.code).toBe('NETWORK');
    expect(fetchFailure.message).toBe(CASES_REVIEW_MESSAGES.downloadNetworkFailed);

    const networkError: CaseDownloadError = toCaseDownloadError(
      new Error('NetworkError when attempting to fetch resource'),
    );
    expect(networkError.code).toBe('NETWORK');
    expect(networkError.message).toBe(CASES_REVIEW_MESSAGES.downloadNetworkFailed);
  });

  it('toCaseDownloadError keeps a GraphQL HTTP message on the generic download failure', (): void => {
    const mapped: CaseDownloadError = toCaseDownloadError(new Error('GraphQL HTTP 502'));
    expect(mapped.message).toBe(CASES_REVIEW_MESSAGES.downloadFailed);
    expect(mapped.code).toBeUndefined();
  });

  it('toCaseDownloadError maps HTTP 429 to a rate-limit message', (): void => {
    const mapped: CaseDownloadError = toCaseDownloadError(
      new HttpErrorResponse({ status: 429, statusText: 'Too Many Requests' }),
    );
    expect(mapped.code).toBe('RATE_LIMITED');
    expect(mapped.message).toBe(CASES_REVIEW_MESSAGES.downloadRateLimited);
  });

  it('toCaseActionError maps an unreachable API to the network message', (): void => {
    const mapped: CaseActionError = toCaseActionError(new Error('GraphQL HTTP 502'));
    expect(mapped.code).toBe('NETWORK');
    expect(mapped.message).toBe(CASES_REVIEW_MESSAGES.actionNetworkFailed);
  });

  it('resolveReviewActions follows status rules for reviewers', (): void => {
    expect(resolveReviewActions('SUBMITTED', 'Reviewer')).toEqual({
      canStartReview: true,
      canApprove: false,
      canReject: false,
    });
    expect(resolveReviewActions('IN_REVIEW', 'TenantAdmin')).toEqual({
      canStartReview: false,
      canApprove: true,
      canReject: true,
    });
    expect(resolveReviewActions('DRAFT', 'Reviewer').canStartReview).toBe(false);
  });

  it('resolveReviewActions hides actions for Customer role', (): void => {
    expect(resolveReviewActions('SUBMITTED', 'Customer')).toEqual({
      canStartReview: false,
      canApprove: false,
      canReject: false,
    });
  });

  it('parseCaseFormData orders known keys', (): void => {
    const fields = parseCaseFormData(
      JSON.stringify({
        address: '1 Road',
        fullName: 'Ada',
        nationality: 'SE',
        dateOfBirth: '1990-01-01',
      }),
    );
    expect(fields.map((f) => f.key)).toEqual([
      'fullName',
      'dateOfBirth',
      'nationality',
      'address',
    ]);
  });

  it('parseCaseDetail maps documents and form fields', (): void => {
    const detail = parseCaseDetail({
      data: {
        case: {
          case: {
            id: '11111111-1111-1111-1111-111111111111',
            title: 'Onboarding',
            status: 'SUBMITTED',
            formData: '{"fullName":"Ada"}',
            customerUserId: '22222222-2222-2222-2222-222222222222',
            customerEmail: 'ada@acme.example',
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-02T00:00:00Z',
            submittedAt: '2026-01-02T00:00:00Z',
            reviewedAt: null,
            reviewedBy: null,
            reviewComment: null,
          },
          comments: [],
          documents: [
            {
              id: '33333333-3333-3333-3333-333333333333',
              fileName: 'id.pdf',
              contentType: 'application/pdf',
              sizeBytes: 1200,
              uploadedAt: '2026-01-02T00:00:00Z',
              uploadedBy: '22222222-2222-2222-2222-222222222222',
            },
          ],
        },
      },
    });

    expect(detail.formFields[0]?.value).toBe('Ada');
    expect(detail.documents).toHaveLength(1);
    expect(detail.documents[0]?.sizeLabel).toBe('1.2 KB');
    expect(detail.documents[0]?.downloadAriaLabel).toBe('Download id.pdf');
  });

  it('normalizeRejectComment requires non-empty trimmed text', (): void => {
    expect(normalizeRejectComment('   ').ok).toBe(false);
    expect(normalizeRejectComment('Missing ID').ok).toBe(true);
  });

  it('toDocumentDownloadUrl builds REST path', (): void => {
    expect(
      toDocumentDownloadUrl(
        'http://localhost:5295/',
        '11111111-1111-1111-1111-111111111111',
        '33333333-3333-3333-3333-333333333333',
      ),
    ).toBe(
      'http://localhost:5295/api/cases/11111111-1111-1111-1111-111111111111/documents/33333333-3333-3333-3333-333333333333',
    );
  });
});
