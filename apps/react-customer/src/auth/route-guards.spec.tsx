import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router';
import { RequireAuth, RequireGuest } from './route-guards';
import { tokenStorage } from './token-storage';

function testJwt(role: string): string {
  const payload: string = btoa(
    JSON.stringify({
      sub: '11111111-1111-1111-1111-111111111111',
      tenant_id: '22222222-2222-2222-2222-222222222222',
      role,
      email: 'user@acme.example',
      exp: Math.floor(Date.now() / 1000) + 3600,
    }),
  )
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
  return `hdr.${payload}.sig`;
}

describe('RequireAuth', () => {
  afterEach((): void => {
    cleanup();
    tokenStorage.clearSession();
  });

  it('redirects guests to login', (): void => {
    render(
      <MemoryRouter initialEntries={['/cases']}>
        <Routes>
          <Route path="/login" element={<div>login-page</div>} />
          <Route
            path="/cases"
            element={
              <RequireAuth>
                <div>cases-page</div>
              </RequireAuth>
            }
          />
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('login-page')).toBeInTheDocument();
  });

  it('renders children for a Customer session', (): void => {
    tokenStorage.setSession(testJwt('Customer'), 'acme');
    render(
      <MemoryRouter initialEntries={['/cases']}>
        <Routes>
          <Route path="/login" element={<div>login-page</div>} />
          <Route
            path="/cases"
            element={
              <RequireAuth>
                <div>cases-page</div>
              </RequireAuth>
            }
          />
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('cases-page')).toBeInTheDocument();
  });

  it('clears a TenantAdmin session and sends them to login', (): void => {
    tokenStorage.setSession(testJwt('TenantAdmin'), 'acme');
    render(
      <MemoryRouter initialEntries={['/cases']}>
        <Routes>
          <Route path="/login" element={<div>login-page</div>} />
          <Route
            path="/cases"
            element={
              <RequireAuth>
                <div>cases-page</div>
              </RequireAuth>
            }
          />
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('login-page')).toBeInTheDocument();
    expect(tokenStorage.getAccessToken()).toBeNull();
  });
});

describe('RequireGuest', () => {
  afterEach((): void => {
    cleanup();
    tokenStorage.clearSession();
  });

  it('sends an authenticated Customer to /cases', (): void => {
    tokenStorage.setSession(testJwt('Customer'), 'acme');
    render(
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route
            path="/login"
            element={
              <RequireGuest>
                <div>login-page</div>
              </RequireGuest>
            }
          />
          <Route path="/cases" element={<div>cases-page</div>} />
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('cases-page')).toBeInTheDocument();
  });
});
