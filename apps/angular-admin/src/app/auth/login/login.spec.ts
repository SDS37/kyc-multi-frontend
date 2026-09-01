import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { MockInstance, vi } from 'vitest';
import { APP_CONFIG } from '../../config/app-config';
import { TokenStorage } from '../token-storage';
import { Login } from './login';

const graphqlUrl: string = 'http://localhost:5295/graphql';

function testAccessToken(role: string = 'Reviewer'): string {
  const header: string = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '');
  const payload: string = btoa(
    JSON.stringify({
      sub: '00000000-0000-0000-0000-000000000001',
      tenant_id: '00000000-0000-0000-0000-000000000002',
      role,
      email: 'reviewer@acme.test',
      exp: Math.floor(Date.now() / 1000) + 3600,
    }),
  )
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '');
  return `${header}.${payload}.sig`;
}

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let httpTesting: HttpTestingController;
  let router: Router;
  let tokens: TokenStorage;

  beforeEach(async (): Promise<void> => {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'cases', children: [] }]),
        {
          provide: APP_CONFIG,
          useValue: {
            apiBaseUrl: 'http://localhost:5295',
            graphqlUrl,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    httpTesting = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    tokens = TestBed.inject(TokenStorage);
    tokens.clearSession();
    fixture.detectChanges();
  });

  afterEach((): void => {
    httpTesting.verify();
    tokens.clearSession();
  });

  it('shows field errors when submitted empty', (): void => {
    submitForm(fixture);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Tenant slug is required');
    expect(fixture.nativeElement.textContent).toContain('Email is required');
    expect(fixture.nativeElement.textContent).toContain('Password is required');
    httpTesting.expectNone(graphqlUrl);
  });

  it('surfaces maxlength errors for overlong fields', (): void => {
    setFormValues(fixture, {
      tenantSlug: 't'.repeat(65),
      email: `${'a'.repeat(250)}@example.com`,
      password: 'p'.repeat(129),
    });
    submitForm(fixture);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Tenant slug must be at most 64 characters.',
    );
    expect(fixture.nativeElement.textContent).toContain('Email must be at most 256 characters.');
    expect(fixture.nativeElement.textContent).toContain(
      'Password must be at most 128 characters.',
    );
    httpTesting.expectNone(graphqlUrl);
  });

  it('stores the token and navigates to /cases on success', (): void => {
    const accessToken: string = testAccessToken();
    const navigateSpy: MockInstance = vi
      .spyOn(router, 'navigateByUrl')
      .mockResolvedValue(true);

    setFormValues(fixture, {
      tenantSlug: 'acme',
      email: 'reviewer@acme.test',
      password: 'Password1!',
    });
    submitForm(fixture);

    httpTesting.expectOne(graphqlUrl).flush({
      data: {
        login: {
          accessToken,
          tokenType: 'Bearer',
          expiresInSeconds: 3600,
        },
      },
    });

    expect(tokens.getAccessToken()).toBe(accessToken);
    expect(navigateSpy).toHaveBeenCalledWith('/cases');
    fixture.detectChanges();
    const submitButton: HTMLButtonElement | null = fixture.nativeElement.querySelector(
      'button[type="submit"]',
    );
    expect(submitButton?.disabled).toBe(false);
  });

  it('surfaces a polite form error on AUTH_FAILED', (): void => {
    setFormValues(fixture, {
      tenantSlug: 'acme',
      email: 'reviewer@acme.test',
      password: 'wrong',
    });
    submitForm(fixture);

    httpTesting.expectOne(graphqlUrl).flush({
      errors: [
        { message: 'Invalid email, password, or tenant.', extensions: { code: 'AUTH_FAILED' } },
      ],
    });
    fixture.detectChanges();

    const alert: Element | null = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert).toBeInstanceOf(HTMLElement);
    expect(alert?.textContent).toContain('Invalid email, password, or tenant.');
    expect(tokens.getAccessToken()).toBeNull();
  });
});

function setFormValues(
  fixture: ComponentFixture<Login>,
  values: { tenantSlug: string; email: string; password: string },
): void {
  const root: HTMLElement = fixture.nativeElement as HTMLElement;
  const inputs: Element[] = Array.from(root.querySelectorAll('input'));
  const tenant: Element | undefined = inputs[0];
  const email: Element | undefined = inputs[1];
  const password: Element | undefined = inputs[2];
  if (
    !(tenant instanceof HTMLInputElement) ||
    !(email instanceof HTMLInputElement) ||
    !(password instanceof HTMLInputElement)
  ) {
    throw new Error('Expected three login inputs');
  }
  setInputValue(tenant, values.tenantSlug);
  setInputValue(email, values.email);
  setInputValue(password, values.password);
  fixture.detectChanges();
}

function setInputValue(input: HTMLInputElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function submitForm(fixture: ComponentFixture<Login>): void {
  const form: Element | null = fixture.nativeElement.querySelector('form');
  if (!(form instanceof HTMLFormElement)) {
    throw new Error('Expected login form');
  }
  form.dispatchEvent(new Event('submit'));
  fixture.detectChanges();
}
