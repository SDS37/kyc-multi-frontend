import { cleanup, render } from '@testing-library/react';
import { afterEach, describe, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router';
import { CaseDraft } from './case-draft';
import * as casesApi from '../cases-api';
import type { CaseDraftDetail } from '../cases.models';

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
      <MemoryRouter initialEntries={['/cases/11111111-1111-1111-1111-111111111111']}>
        <Routes>
          <Route path="/cases/:caseId" element={<CaseDraft />} />
        </Routes>
      </MemoryRouter>,
    );
    unmount();
    finish?.({
      id: '11111111-1111-1111-1111-111111111111',
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
    });
    await Promise.resolve();
  });
});
