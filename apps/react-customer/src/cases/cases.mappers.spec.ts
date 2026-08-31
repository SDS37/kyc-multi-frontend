import { describe, expect, it } from 'vitest';
import {
  parseCasesPage,
  parseCreatedDraft,
  parseStatusFilterValue,
  toCreateDraftVariables,
  toListCasesVariables,
  validateCreateDraftTitle,
} from './cases.mappers';
import { CasesLoadError, CreateDraftError } from './cases.models';

describe('cases.mappers', () => {
  it('toListCasesVariables defaults pagination', (): void => {
    expect(toListCasesVariables()).toEqual({ status: null, skip: 0, take: 20 });
    expect(toListCasesVariables({ status: 'DRAFT', skip: 5, take: 10 })).toEqual({
      status: 'DRAFT',
      skip: 5,
      take: 10,
    });
  });

  it('parseCasesPage maps items without customerEmail', (): void => {
    const page = parseCasesPage(
      {
        data: {
          cases: {
            totalCount: 1,
            skip: 0,
            take: 20,
            items: [
              {
                id: 'c1',
                title: 'Onboarding',
                status: 'DRAFT',
                updatedAt: '2026-01-02T03:04:05Z',
              },
            ],
          },
        },
      },
      { status: null, skip: 0, take: 20 },
    );
    expect(page.totalCount).toBe(1);
    expect(page.items[0]?.statusLabel).toBe('Draft');
    expect(page.items[0]?.openAriaLabel).toContain('Onboarding');
  });

  it('parseCasesPage throws on GraphQL errors', (): void => {
    expect(() =>
      parseCasesPage(
        { errors: [{ message: 'Nope', extensions: { code: 'AUTH_FAILED' } }] },
        { status: null, skip: 0, take: 20 },
      ),
    ).toThrow(CasesLoadError);
  });

  it('parseStatusFilterValue accepts null and known statuses', (): void => {
    expect(parseStatusFilterValue(null)).toBeNull();
    expect(parseStatusFilterValue('')).toBeNull();
    expect(parseStatusFilterValue('SUBMITTED')).toBe('SUBMITTED');
    expect(parseStatusFilterValue('nope')).toBeUndefined();
  });

  it('validateCreateDraftTitle enforces required and max length', (): void => {
    expect(validateCreateDraftTitle('  ')).not.toBeNull();
    expect(validateCreateDraftTitle('Ok')).toBeNull();
    expect(validateCreateDraftTitle('x'.repeat(201))).not.toBeNull();
  });

  it('toCreateDraftVariables only sends title', (): void => {
    expect(toCreateDraftVariables({ title: '  Hello  ' })).toEqual({
      input: { title: 'Hello' },
    });
  });

  it('parseCreatedDraft maps AUTH_NOT_AUTHORIZED', (): void => {
    expect(() =>
      parseCreatedDraft({
        errors: [{ message: 'Denied', extensions: { code: 'AUTH_NOT_AUTHORIZED' } }],
      }),
    ).toThrow(CreateDraftError);
  });
});
