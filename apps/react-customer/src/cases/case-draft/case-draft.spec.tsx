import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router';
import { CaseDraft } from './case-draft';
import * as casesApi from '../cases-api';
import { CASES_DRAFT_MESSAGES } from '../cases.messages';
import type { CaseDraftDetail } from '../cases.models';

const draftId: string = '11111111-1111-1111-1111-111111111111';

const draftDetail: CaseDraftDetail = {
  id: draftId,
  title: 'x',
  status: 'DRAFT',
  statusLabel: 'Draft',
  formDataRaw: '{}',
  form: {
    title: 'x',
    fullName: '',
    dateOfBirth: '',
    nationality: '',
    address: '',
    companyName: '',
  },
  updatedAt: '2026-01-01T00:00:00Z',
  updatedAtLabel: '1 Jan 2026',
  submittedAt: null,
  submittedAtLabel: null,
  documents: [],
  canEdit: true,
  canUpload: true,
};

describe('CaseDraft', () => {
  afterEach((): void => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('does not apply a stale detail load after unmount', async (): Promise<void> => {
    let finish: ((detail: CaseDraftDetail) => void) | undefined;
    vi.spyOn(casesApi, 'getCaseDetail').mockImplementation(
      (): Promise<CaseDraftDetail> =>
        new Promise((resolve: (detail: CaseDraftDetail) => void): void => {
          finish = resolve;
        }),
    );

    const { unmount } = render(
      <MemoryRouter initialEntries={[`/cases/${draftId}`]}>
        <Routes>
          <Route path="/cases/:caseId" element={<CaseDraft />} />
        </Routes>
      </MemoryRouter>,
    );
    unmount();
    finish?.(draftDetail);
    await Promise.resolve();
  });

  it('does not apply a stale save after unmount', async (): Promise<void> => {
    vi.spyOn(casesApi, 'getCaseDetail').mockResolvedValue(draftDetail);
    let finishSave: ((detail: CaseDraftDetail) => void) | undefined;
    vi.spyOn(casesApi, 'updateDraftCase').mockImplementation(
      (): Promise<CaseDraftDetail> =>
        new Promise((resolve: (detail: CaseDraftDetail) => void): void => {
          finishSave = resolve;
        }),
    );

    const { unmount } = render(
      <MemoryRouter initialEntries={[`/cases/${draftId}`]}>
        <Routes>
          <Route path="/cases/:caseId" element={<CaseDraft />} />
        </Routes>
      </MemoryRouter>,
    );

    const save: HTMLElement = await screen.findByRole('button', {
      name: CASES_DRAFT_MESSAGES.saveDraft,
    });
    await userEvent.click(save);
    await waitFor((): void => {
      if (finishSave === undefined) {
        throw new Error('expected updateDraftCase to start');
      }
    });
    unmount();
    finishSave?.(draftDetail);
    await Promise.resolve();
  });
});
