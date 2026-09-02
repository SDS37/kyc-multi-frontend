import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router';
import { SHELL_MESSAGES } from './shell.messages';
import { CustomerShell } from './customer-shell';

describe('CustomerShell', () => {
  afterEach((): void => {
    cleanup();
  });

  it('exposes a skip link to main content', (): void => {
    render(
      <MemoryRouter>
        <Routes>
          <Route element={<CustomerShell />}>
            <Route index element={<div>page</div>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );
    const skip: HTMLAnchorElement = screen.getByRole('link', {
      name: SHELL_MESSAGES.skipToContent,
    });
    expect(skip.getAttribute('href')).toBe('#main');
    expect(document.getElementById('main')).not.toBeNull();
  });
});
