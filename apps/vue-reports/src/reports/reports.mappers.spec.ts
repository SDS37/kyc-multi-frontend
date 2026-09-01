import { describe, expect, it } from 'vitest';
import { parseReportsOverview, toReportsLoadError } from './reports.mappers';
import { REPORTS_HOME_MESSAGES } from './reports.messages';
import { ReportsLoadError, type ReportsOverview } from './reports.models';

function countPage(totalCount: number): { totalCount: number } {
  return { totalCount };
}

function overviewData(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    draft: countPage(1),
    submitted: countPage(2),
    inReview: countPage(3),
    approved: countPage(4),
    rejected: countPage(5),
    latest: {
      totalCount: 6,
      items: [
        {
          id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          title: 'Passport check',
          status: 'SUBMITTED',
          customerEmail: 'c@acme.example',
          updatedAt: '2026-09-01T12:00:00.000Z',
        },
      ],
    },
    ...overrides,
  };
}

describe('parseReportsOverview', () => {
  it('maps aliased counts and latest rows', (): void => {
    const parsed: ReportsOverview = parseReportsOverview({ data: overviewData() });
    expect(parsed.counts.map((c) => [c.status, c.count])).toEqual([
      ['DRAFT', 1],
      ['SUBMITTED', 2],
      ['IN_REVIEW', 3],
      ['APPROVED', 4],
      ['REJECTED', 5],
    ]);
    expect(parsed.latest).toHaveLength(1);
    expect(parsed.latest[0]?.title).toBe('Passport check');
    expect(parsed.latest[0]?.statusLabel).toBe('Submitted');
    expect(parsed.latest[0]?.customerEmail).toBe('c@acme.example');
    expect(parsed.latestTotalCount).toBe(6);
  });

  it('caps latest items at 10', (): void => {
    const items = [...Array(12).keys()].map((index: number) => ({
      id: `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa${String(index).padStart(2, '0')}`,
      title: `Case ${String(index)}`,
      status: 'DRAFT',
      customerEmail: 'c@acme.example',
      updatedAt: '2026-09-01T12:00:00.000Z',
    }));
    const parsed = parseReportsOverview({
      data: overviewData({ latest: { totalCount: 12, items } }),
    });
    expect(parsed.latest).toHaveLength(10);
  });

  it('throws ReportsLoadError on GraphQL errors', (): void => {
    expect(() =>
      parseReportsOverview({
        errors: [{ message: 'Denied.', extensions: { code: 'AUTH_NOT_AUTHORIZED' } }],
      }),
    ).toThrow(ReportsLoadError);
  });

  it('throws on an unexpected status', (): void => {
    expect(() =>
      parseReportsOverview({
        data: overviewData({
          latest: {
            totalCount: 1,
            items: [
              {
                id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
                title: 'Bad',
                status: 'NOPE',
                customerEmail: 'c@acme.example',
                updatedAt: '2026-09-01T12:00:00.000Z',
              },
            ],
          },
        }),
      }),
    ).toThrow(/Unexpected case status/);
  });

  it('throws when a count alias is missing', (): void => {
    expect(() =>
      parseReportsOverview({
        data: overviewData({ draft: undefined }),
      }),
    ).toThrow(ReportsLoadError);
  });

  it('throws when a status totalCount is not an integer', (): void => {
    expect(() =>
      parseReportsOverview({
        data: overviewData({ draft: countPage(1.5) }),
      }),
    ).toThrow(ReportsLoadError);
  });
});

describe('toReportsLoadError', () => {
  it('maps network failures', (): void => {
    const mapped: ReportsLoadError = toReportsLoadError(new TypeError('Failed to fetch'));
    expect(mapped.code).toBe('NETWORK');
    expect(mapped.message).toBe(REPORTS_HOME_MESSAGES.listLoadFailed);
  });

  it('passes ReportsLoadError through', (): void => {
    const original: ReportsLoadError = new ReportsLoadError('x', 'AUTH_FAILED');
    expect(toReportsLoadError(original)).toBe(original);
  });
});
