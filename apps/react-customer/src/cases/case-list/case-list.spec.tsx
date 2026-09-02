import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router';
import { CaseList } from './case-list';
import * as casesApi from '../cases-api';
import type { CaseListPage } from '../cases.models';

describe('CaseList', () => {
  afterEach((): void => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('does not apply a stale list load after unmount', async (): Promise<void> => {
    let finish: ((page: CaseListPage) => void) | undefined;
    vi.spyOn(casesApi, 'listCases').mockImplementation(
      (): Promise<CaseListPage> =>
        new Promise((resolve: (page: CaseListPage) => void): void => {
          finish = resolve;
        }),
    );

    const { unmount } = render(
      <MemoryRouter>
        <CaseList />
      </MemoryRouter>,
    );
    unmount();
    finish?.({ items: [], totalCount: 0, skip: 0, take: 20 });
    await Promise.resolve();
  });
});
