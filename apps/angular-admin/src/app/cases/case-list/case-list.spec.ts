import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_CONFIG } from '../../config/app-config';
import { TokenStorage } from '../../auth/token-storage';
import { CaseList } from './case-list';

const graphqlUrl: string = 'http://localhost:5295/graphql';

describe('CaseList', () => {
  let fixture: ComponentFixture<CaseList>;
  let httpTesting: HttpTestingController;
  let tokens: TokenStorage;

  beforeEach(async (): Promise<void> => {
    await TestBed.configureTestingModule({
      imports: [CaseList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
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
    fixture = TestBed.createComponent(CaseList);
    fixture.detectChanges();
  });

  afterEach((): void => {
    httpTesting.verify();
    tokens.clearSession();
  });

  it('shows loading then rows with title, customer, status, and updated date', (): void => {
    expect(fixture.nativeElement.textContent).toContain('Loading cases');

    httpTesting.expectOne(graphqlUrl).flush({
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
              updatedAt: '2026-08-30T12:00:00.000Z',
            },
          ],
        },
      },
    });
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent as string;
    expect(text).toContain('Acme onboarding');
    expect(text).toContain('customer@acme.example');
    expect(text).toContain('Submitted');
    expect(text).toContain('1 case');
  });

  it('shows empty state when there are no cases', (): void => {
    httpTesting.expectOne(graphqlUrl).flush({
      data: {
        cases: { totalCount: 0, skip: 0, take: 20, items: [] },
      },
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No cases yet for this tenant');
  });

  it('reloads when status filter changes', (): void => {
    httpTesting.expectOne(graphqlUrl).flush({
      data: { cases: { totalCount: 0, skip: 0, take: 20, items: [] } },
    });
    fixture.detectChanges();

    fixture.componentInstance['filterStatus']('IN_REVIEW');

    const req = httpTesting.expectOne(graphqlUrl);
    expect(req.request.body.variables.status).toBe('IN_REVIEW');
    req.flush({
      data: { cases: { totalCount: 0, skip: 0, take: 20, items: [] } },
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No cases with status In review');
  });

  it('surfaces load errors with try again', (): void => {
    httpTesting.expectOne(graphqlUrl).flush({
      errors: [{ message: 'Unable to load cases from API.', extensions: { code: 'AUTH_FAILED' } }],
    });
    fixture.detectChanges();

    const alert: Element | null = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Unable to load cases from API.');

    fixture.componentInstance['reload']();
    httpTesting.expectOne(graphqlUrl).flush({
      data: { cases: { totalCount: 0, skip: 0, take: 20, items: [] } },
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No cases yet for this tenant');
  });

  it('ignores stale responses when reloads overlap', (): void => {
    const firstReq = httpTesting.expectOne(graphqlUrl);

    fixture.componentInstance['filterStatus']('IN_REVIEW');
    const secondReq = httpTesting.expectOne(graphqlUrl);
    expect(secondReq.request.body.variables.status).toBe('IN_REVIEW');

    secondReq.flush({
      data: {
        cases: {
          totalCount: 1,
          skip: 0,
          take: 20,
          items: [
            {
              id: '22222222-2222-2222-2222-222222222222',
              title: 'In review only',
              status: 'IN_REVIEW',
              customerEmail: 'review@acme.example',
              updatedAt: '2026-08-30T13:00:00.000Z',
            },
          ],
        },
      },
    });
    fixture.detectChanges();

    expect(firstReq.cancelled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('In review only');
  });
});
