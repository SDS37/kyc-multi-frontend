import { afterEach, describe, expect, it, vi } from 'vitest';
import * as http from '../shared/http';
import { createDraftCase, listCases } from './cases-api';

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
});
