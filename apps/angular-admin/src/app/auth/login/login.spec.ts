import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { APP_CONFIG } from '../../config/app-config';
import { TokenStorage } from '../token-storage';
import { Login } from './login';

const graphqlUrl = 'http://localhost:5295/graphql';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let httpTesting: HttpTestingController;
  let router: Router;
  let tokens: TokenStorage;

  beforeEach(async () => {
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
    tokens.clearAccessToken();
    fixture.detectChanges();
  });

  afterEach(() => {
    httpTesting.verify();
    tokens.clearAccessToken();
  });

  it('shows field errors when submitted empty', () => {
    submitForm(fixture);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Tenant slug is required');
    expect(fixture.nativeElement.textContent).toContain('Email is required');
    expect(fixture.nativeElement.textContent).toContain('Password is required');
    httpTesting.expectNone(graphqlUrl);
  });

  it('stores the token and navigates to /cases on success', () => {
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    setFormValues(fixture, {
      tenantSlug: 'acme',
      email: 'reviewer@acme.test',
      password: 'Password1!',
    });
    submitForm(fixture);

    httpTesting.expectOne(graphqlUrl).flush({
      data: {
        login: {
          accessToken: 'jwt-from-api',
          tokenType: 'Bearer',
          expiresInSeconds: 3600,
        },
      },
    });

    expect(tokens.getAccessToken()).toBe('jwt-from-api');
    expect(navigateSpy).toHaveBeenCalledWith('/cases');
  });

  it('surfaces a polite form error on AUTH_FAILED', () => {
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

    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    expect(alert?.textContent).toContain('Invalid email, password, or tenant.');
    expect(tokens.getAccessToken()).toBeNull();
  });
});

function setFormValues(
  fixture: ComponentFixture<Login>,
  values: { tenantSlug: string; email: string; password: string },
): void {
  const root = fixture.nativeElement as HTMLElement;
  const [tenant, email, password] = Array.from(root.querySelectorAll('input'));
  setInputValue(tenant, values.tenantSlug);
  setInputValue(email, values.email);
  setInputValue(password, values.password);
  fixture.detectChanges();
}

function setInputValue(input: Element | undefined, value: string): void {
  const el = input as HTMLInputElement;
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

function submitForm(fixture: ComponentFixture<Login>): void {
  const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
  form.dispatchEvent(new Event('submit'));
  fixture.detectChanges();
}
