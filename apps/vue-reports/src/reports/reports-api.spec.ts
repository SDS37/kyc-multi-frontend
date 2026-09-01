import { afterEach, describe, expect, it, vi } from 'vitest';
import * as http from '../shared/http';
import { loadReportsOverview } from './reports-api';
import { ReportsLoadError } from './reports.models';

describe('loadReportsOverview', () => {
  afterEach((): void => {
    vi.restoreAllMocks();
  });

  it('posts the aliased cases query without skipAuth', async (): Promise<void> => {
    vi.spyOn(http, 'graphqlRequest').mockResolvedValue({
      data: {
        draft: { totalCount: 0 },
        submitted: { totalCount: 0 },
        inReview: { totalCount: 0 },
        approved: { totalCount: 0 },
        rejected: { totalCount: 0 },
        latest: { totalCount: 0, items: [] },
      },
    });

    const overview = await loadReportsOverview();
    expect(overview.latest).toEqual([]);
    expect(http.graphqlRequest).toHaveBeenCalledWith(
      expect.stringContaining('query ReportsOverview'),
    );
    expect(http.graphqlRequest).toHaveBeenCalledWith(expect.stringContaining('draft: cases'));
    expect(http.graphqlRequest).not.toHaveBeenCalledWith(
      expect.anything(),
      expect.anything(),
      expect.objectContaining({ skipAuth: true }),
    );
  });

  it('wraps GraphQL HTTP failures as ReportsLoadError', async (): Promise<void> => {
    vi.spyOn(http, 'graphqlRequest').mockRejectedValue(new Error('GraphQL HTTP 500'));
    await expect(loadReportsOverview()).rejects.toBeInstanceOf(ReportsLoadError);
  });
});
