import { afterEach, describe, expect, it, vi } from 'vitest';
import * as http from '../shared/http';
import {
  createDraftCase,
  getCaseDetail,
  listCases,
  submitCase,
  updateDraftCase,
} from './cases-api';
import type { CaseDraftDetail } from './cases.models';

describe('cases-api', () => {
  afterEach((): void => {
    vi.restoreAllMocks();
  });

  it('listCases calls GraphQL without skipAuth', async (): Promise<void> => {
    const spy = vi.spyOn(http, 'graphqlRequest').mockResolvedValue({
      data: {
        cases: {
          totalCount: 0,
          skip: 0,
          take: 20,
          items: [],
        },
      },
    });

    await listCases({ status: 'DRAFT' });

    expect(spy).toHaveBeenCalledTimes(1);
    expect(spy.mock.calls[0]?.[2]).toBeUndefined();
    expect(spy.mock.calls[0]?.[1]).toEqual({
      status: 'DRAFT',
      skip: 0,
      take: 20,
    });
  });

  it('createDraftCase sends title only', async (): Promise<void> => {
    const spy = vi.spyOn(http, 'graphqlRequest').mockResolvedValue({
      data: {
        createDraftCase: {
          id: 'c1',
          title: 'Acme',
          status: 'DRAFT',
          updatedAt: '2026-01-01T00:00:00Z',
        },
      },
    });

    const created = await createDraftCase({ title: ' Acme ' });
    expect(created.id).toBe('c1');
    expect(spy.mock.calls[0]?.[1]).toEqual({ input: { title: 'Acme' } });
    expect(spy.mock.calls[0]?.[2]).toBeUndefined();
  });

  it('getCaseDetail / updateDraftCase / submitCase omit skipAuth', async (): Promise<void> => {
    const caseId: string = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
    const detailBody = {
      data: {
        case: {
          case: {
            id: caseId,
            title: 'Onboarding',
            status: 'DRAFT',
            formData: '{"fullName":"Ada"}',
            updatedAt: '2026-01-02T03:04:05Z',
            submittedAt: null,
          },
        },
      },
    };

    const getSpy = vi.spyOn(http, 'graphqlRequest').mockResolvedValue(detailBody);
    const loaded = await getCaseDetail(caseId);
    expect(loaded.form.fullName).toBe('Ada');
    expect(getSpy.mock.calls[0]?.[2]).toBeUndefined();

    getSpy.mockResolvedValue({
      data: {
        updateDraftCase: {
          id: caseId,
          title: 'Onboarding',
          status: 'DRAFT',
          formData: '{"fullName":"Ada Lovelace"}',
          updatedAt: '2026-01-02T04:00:00Z',
          submittedAt: null,
        },
      },
    });
    const saved = await updateDraftCase(caseId, {
      title: 'Onboarding',
      fullName: 'Ada Lovelace',
      dateOfBirth: '',
      nationality: '',
      address: '',
      companyName: '',
    });
    expect(saved.form.fullName).toBe('Ada Lovelace');
    expect(getSpy.mock.calls[1]?.[1]).toEqual({
      input: {
        id: caseId,
        title: 'Onboarding',
        formData: '{"fullName":"Ada Lovelace"}',
      },
    });
    expect(getSpy.mock.calls[1]?.[2]).toBeUndefined();

    const previous: CaseDraftDetail = saved;
    getSpy.mockResolvedValue({
      data: {
        submitCase: {
          id: caseId,
          status: 'SUBMITTED',
          submittedAt: '2026-01-03T00:00:00Z',
          updatedAt: '2026-01-03T00:00:00Z',
        },
      },
    });
    const submitted = await submitCase(caseId, previous);
    expect(submitted.status).toBe('SUBMITTED');
    expect(getSpy.mock.calls[2]?.[1]).toEqual({ input: { id: caseId } });
    expect(getSpy.mock.calls[2]?.[2]).toBeUndefined();
  });
});
