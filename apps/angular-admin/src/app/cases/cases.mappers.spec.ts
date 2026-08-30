import { HttpErrorResponse } from '@angular/common/http';
import {
  parseCasesPage,
  parseStatusFilterValue,
  toCasesLoadError,
  toListCasesVariables,
} from './cases.mappers';
import { CasesLoadError } from './cases.models';

describe('cases.mappers', () => {
  it('toListCasesVariables applies defaults', (): void => {
    expect(toListCasesVariables({})).toEqual({
      status: null,
      skip: 0,
      take: 20,
    });
  });

  it('parseCasesPage maps items', (): void => {
    const page = parseCasesPage(
      {
        data: {
          cases: {
            totalCount: 1,
            skip: 0,
            take: 20,
            items: [
              {
                id: '1',
                title: 'T',
                status: 'DRAFT',
                customerEmail: 'a@b.c',
                updatedAt: '2026-01-01T00:00:00Z',
              },
            ],
          },
        },
      },
      { status: null, skip: 0, take: 20 },
    );

    expect(page.items).toHaveLength(1);
    expect(page.items[0]?.status).toBe('DRAFT');
  });

  it('parseCasesPage rejects unknown status', (): void => {
    expect(() =>
      parseCasesPage(
        {
          data: {
            cases: {
              totalCount: 1,
              skip: 0,
              take: 20,
              items: [
                {
                  id: '1',
                  title: 'T',
                  status: 'NOPE',
                  customerEmail: 'a@b.c',
                  updatedAt: '2026-01-01T00:00:00Z',
                },
              ],
            },
          },
        },
        { status: null, skip: 0, take: 20 },
      ),
    ).toThrow(CasesLoadError);
  });

  it('parseStatusFilterValue accepts null and known statuses', (): void => {
    expect(parseStatusFilterValue(null)).toBeNull();
    expect(parseStatusFilterValue('IN_REVIEW')).toBe('IN_REVIEW');
    expect(parseStatusFilterValue('nope')).toBeUndefined();
  });

  it('toCasesLoadError maps network failures', (): void => {
    expect(toCasesLoadError(new HttpErrorResponse({ status: 0 })).code).toBe('NETWORK');
  });
});
