import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { APP_CONFIG } from '../../config/app-config';
import { TokenStorage } from '../../auth/token-storage';
import { CaseReview } from './case-review';

const graphqlUrl: string = 'http://localhost:5295/graphql';
const caseId: string = '11111111-1111-1111-1111-111111111111';

describe('CaseReview', () => {
  let fixture: ComponentFixture<CaseReview>;
  let httpTesting: HttpTestingController;
  let tokens: TokenStorage;

  beforeEach(async (): Promise<void> => {
    await TestBed.configureTestingModule({
      imports: [CaseReview],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ caseId }),
            },
          },
        },
        {
          provide: APP_CONFIG,
          useValue: {
            apiBaseUrl: 'http://localhost:5295',
            graphqlUrl,
          },
        },
      ],
    }).compileComponents();

    tokens = TestBed.inject(TokenStorage);
    tokens.setAccessToken('jwt');
    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CaseReview);
    fixture.detectChanges();
  });

  afterEach((): void => {
    httpTesting.verify();
    tokens.clearSession();
  });

  async function flushDetail(status: string = 'SUBMITTED'): Promise<void> {
    httpTesting.expectOne(graphqlUrl).flush({
      data: {
        case: {
          case: {
            id: caseId,
            title: 'Acme onboarding',
            status,
            formData: JSON.stringify({
              fullName: 'Ada Lovelace',
              dateOfBirth: '1815-12-10',
              nationality: 'GB',
              address: 'London',
            }),
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
              fileName: 'passport.pdf',
              contentType: 'application/pdf',
              sizeBytes: 2048,
              uploadedAt: '2026-01-02T00:00:00Z',
              uploadedBy: '22222222-2222-2222-2222-222222222222',
            },
          ],
        },
      },
    });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows form data and documents for a submitted case', async (): Promise<void> => {
    expect(fixture.nativeElement.textContent).toContain('Loading case');
    await flushDetail('SUBMITTED');

    const text: string = fixture.nativeElement.textContent as string;
    expect(text).toContain('Acme onboarding');
    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('passport.pdf');
    expect(text).toContain('Start review');
    expect(text).not.toContain('Approve');
  });

  it('shows approve and reject when in review', async (): Promise<void> => {
    await flushDetail('IN_REVIEW');
    const text: string = fixture.nativeElement.textContent as string;
    expect(text).toContain('Approve');
    expect(text).toContain('Reject');
    expect(text).toContain('Reject comment (required)');
  });

  it('requires a reject comment before calling the API', async (): Promise<void> => {
    await flushDetail('IN_REVIEW');
    fixture.componentInstance['reject']();
    fixture.detectChanges();

    const alertText: string =
      (fixture.nativeElement.querySelector('[role="alert"]')?.textContent as string | undefined) ??
      (fixture.nativeElement.textContent as string);
    expect(alertText).toContain('A comment is required');
    httpTesting.expectNone(graphqlUrl);
  });

  it('shows an error when the case is not found', async (): Promise<void> => {
    expect(fixture.nativeElement.textContent).toContain('Loading case');
    httpTesting.expectOne(graphqlUrl).flush({
      errors: [{ message: 'Case was not found.', extensions: { code: 'NOT_FOUND' } }],
      data: null,
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const alertText: string =
      (fixture.nativeElement.querySelector('[role="alert"]')?.textContent as string | undefined) ??
      '';
    expect(alertText).toContain('Case was not found');
    expect(fixture.nativeElement.textContent).not.toContain('Loading case');
  });

  it('starts review then reloads detail', async (): Promise<void> => {
    await flushDetail('SUBMITTED');
    fixture.componentInstance['startReview']();

    httpTesting.expectOne(graphqlUrl).flush({
      data: {
        startCaseReview: {
          id: caseId,
          status: 'IN_REVIEW',
          updatedAt: '2026-01-03T00:00:00Z',
        },
      },
    });
    TestBed.inject(ApplicationRef).tick();
    await flushDetail('IN_REVIEW');
    expect(fixture.nativeElement.textContent).toContain('Approve');
  });
});
