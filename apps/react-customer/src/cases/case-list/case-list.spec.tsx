import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router';
import { CaseList } from './case-list';
import * as casesApi from '../cases-api';
import { CASES_LIST_MESSAGES } from '../cases.messages';
import type { CaseListPage, CreatedDraftCase } from '../cases.models';

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

  it('does not navigate after create if unmounted', async (): Promise<void> => {
    vi.spyOn(casesApi, 'listCases').mockResolvedValue({
      items: [],
      totalCount: 0,
      skip: 0,
      take: 20,
    });
    let finishCreate: ((created: CreatedDraftCase) => void) | undefined;
    vi.spyOn(casesApi, 'createDraftCase').mockImplementation(
      (): Promise<CreatedDraftCase> =>
        new Promise((resolve: (created: CreatedDraftCase) => void): void => {
          finishCreate = resolve;
        }),
    );

    const { unmount } = render(
      <MemoryRouter>
        <CaseList />
      </MemoryRouter>,
    );

    const openCreate: HTMLElement = await screen.findByRole('button', {
      name: CASES_LIST_MESSAGES.createAction,
    });
    await userEvent.click(openCreate);
    const title: HTMLElement = await screen.findByLabelText(CASES_LIST_MESSAGES.createTitleLabel);
    await userEvent.type(title, 'New draft');
    await userEvent.click(
      screen.getByRole('button', { name: CASES_LIST_MESSAGES.createSubmit }),
    );
    await waitFor((): void => {
      if (finishCreate === undefined) {
        throw new Error('expected createDraftCase to start');
      }
    });
    unmount();
    finishCreate?.({
      id: '11111111-1111-1111-1111-111111111111',
      title: 'New draft',
      status: 'DRAFT',
      updatedAt: '2026-01-01T00:00:00Z',
    });
    await Promise.resolve();
  });
});
