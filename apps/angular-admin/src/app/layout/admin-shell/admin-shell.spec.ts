import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { MockInstance, vi } from 'vitest';
import { TokenStorage } from '../../auth/token-storage';
import { AdminShell } from './admin-shell';

function makeToken(claims: Record<string, string | number>): string {
  const payload: string = btoa(
    JSON.stringify({
      ...claims,
      exp: Math.floor(Date.now() / 1000) + 3600,
    }),
  )
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
  return `hdr.${payload}.sig`;
}

describe('AdminShell', () => {
  let fixture: ComponentFixture<AdminShell>;
  let tokens: TokenStorage;
  let router: Router;

  beforeEach(async (): Promise<void> => {
    await TestBed.configureTestingModule({
      imports: [AdminShell],
      providers: [provideRouter([{ path: 'login', children: [] }])],
    }).compileComponents();

    tokens = TestBed.inject(TokenStorage);
    router = TestBed.inject(Router);
    tokens.clearSession();

    tokens.setSession(
      makeToken({
        sub: '11111111-1111-1111-1111-111111111111',
        tenant_id: '22222222-2222-2222-2222-222222222222',
        role: 'TenantAdmin',
        email: 'admin@acme.example',
      }),
      'acme',
    );

    fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
  });

  afterEach((): void => {
    tokens.clearSession();
  });

  it('shows tenant slug, user email, role, and Cases nav', (): void => {
    const text: string = fixture.nativeElement.textContent as string;
    expect(text).toContain('KYC Admin');
    expect(text).toContain('Cases');
    expect(text).toContain('acme');
    expect(text).toContain('admin@acme.example');
    expect(text).toContain('Tenant admin');
    expect(text).toContain('Sign out');
  });

  it('exposes a skip link to main content', (): void => {
    const skip: HTMLAnchorElement | null = fixture.nativeElement.querySelector('a.shell__skip');
    expect(skip).not.toBeNull();
    expect(skip?.getAttribute('href')).toBe('#main');
    expect(skip?.textContent?.trim()).toBe('Skip to main content');
    expect(fixture.nativeElement.querySelector('main#main')).not.toBeNull();
  });

  it('signs out and navigates to login', (): void => {
    const navigateSpy: MockInstance = vi
      .spyOn(router, 'navigateByUrl')
      .mockResolvedValue(true);

    const button: HTMLButtonElement | null = fixture.nativeElement.querySelector(
      'button',
    );
    expect(button).not.toBeNull();
    button!.click();
    fixture.detectChanges();

    expect(tokens.getAccessToken()).toBeNull();
    expect(tokens.getTenantSlug()).toBeNull();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});
