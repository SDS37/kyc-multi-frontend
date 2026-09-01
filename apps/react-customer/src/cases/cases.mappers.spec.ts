import { describe, expect, it } from 'vitest';
import {
  draftFormToFormDataJson,
  emptyDraftForm,
  hasDraftFieldErrors,
  isCaseId,
  parseCaseDraftDetail,
  parseCasesPage,
  parseCreatedDraft,
  parseFormDataToDraftForm,
  parseStatusFilterValue,
  parseSubmittedCase,
  parseUpdatedDraft,
  toCreateDraftVariables,
  toListCasesVariables,
  toUpdateDraftVariables,
  validateCreateDraftTitle,
  validateDraftSave,
  validateDraftSubmit,
} from './cases.mappers';
import { CasesLoadError, CreateDraftError, DraftActionError } from './cases.models';

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

  it('isCaseId accepts UUID shapes', (): void => {
    expect(isCaseId('not-a-uuid')).toBe(false);
    expect(isCaseId('aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee')).toBe(true);
  });

  it('parseFormDataToDraftForm reads known keys and ignores junk', (): void => {
    const form = parseFormDataToDraftForm(
      'Title',
      JSON.stringify({
        fullName: 'Ada',
        dateOfBirth: '1815-12-10',
        nationality: 'GB',
        address: 'London',
        companyName: 'Analytical',
        extra: 'ignored',
      }),
    );
    expect(form).toEqual({
      title: 'Title',
      fullName: 'Ada',
      dateOfBirth: '1815-12-10',
      nationality: 'GB',
      address: 'London',
      companyName: 'Analytical',
    });
    expect(parseFormDataToDraftForm('T', 'not-json')).toEqual(emptyDraftForm('T'));
  });

  it('draftFormToFormDataJson omits blanks and never sends title', (): void => {
    expect(
      JSON.parse(
        draftFormToFormDataJson({
          title: 'Kept separately',
          fullName: ' Ada ',
          dateOfBirth: '',
          nationality: 'GB',
          address: '  ',
          companyName: ' Co ',
        }),
      ),
    ).toEqual({
      fullName: 'Ada',
      nationality: 'GB',
      companyName: 'Co',
    });
  });

  it('validateDraftSave allows empty person fields but checks DOB format', (): void => {
    expect(hasDraftFieldErrors(validateDraftSave(emptyDraftForm('Ok')))).toBe(false);
    expect(validateDraftSave(emptyDraftForm('')).title).toBeDefined();
    expect(
      validateDraftSave({
        ...emptyDraftForm('Ok'),
        dateOfBirth: '31-12-1815',
      }).dateOfBirth,
    ).toBeDefined();
  });

  it('validateDraftSubmit requires person fields', (): void => {
    const errors = validateDraftSubmit(emptyDraftForm('Ok'));
    expect(errors.fullName).toBeDefined();
    expect(errors.dateOfBirth).toBeDefined();
    expect(errors.nationality).toBeDefined();
    expect(errors.address).toBeDefined();

    const valid = validateDraftSubmit({
      title: 'Ok',
      fullName: 'Ada',
      dateOfBirth: '1815-12-10',
      nationality: 'GB',
      address: 'London',
      companyName: '',
    });
    expect(hasDraftFieldErrors(valid)).toBe(false);
  });

  it('parseCaseDraftDetail maps DRAFT as editable', (): void => {
    const detail = parseCaseDraftDetail({
      data: {
        case: {
          case: {
            id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
            title: 'Onboarding',
            status: 'DRAFT',
            formData: '{"fullName":"Ada"}',
            updatedAt: '2026-01-02T03:04:05Z',
            submittedAt: null,
          },
        },
      },
    });
    expect(detail.canEdit).toBe(true);
    expect(detail.form.fullName).toBe('Ada');
    expect(detail.submittedAtLabel).toBeNull();
  });

  it('toUpdateDraftVariables never includes tenant ids', (): void => {
    expect(
      toUpdateDraftVariables({
        id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
        title: '  Hello  ',
        formData: '{"fullName":"Ada"}',
      }),
    ).toEqual({
      input: {
        id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
        title: 'Hello',
        formData: '{"fullName":"Ada"}',
      },
    });
  });

  it('parseUpdatedDraft and parseSubmittedCase map action errors', (): void => {
    expect(() =>
      parseUpdatedDraft({
        errors: [{ message: 'Locked', extensions: { code: 'DOMAIN' } }],
      }),
    ).toThrow(DraftActionError);

    const previous = parseCaseDraftDetail({
      data: {
        case: {
          case: {
            id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
            title: 'Onboarding',
            status: 'DRAFT',
            formData: '{}',
            updatedAt: '2026-01-02T03:04:05Z',
            submittedAt: null,
          },
        },
      },
    });

    const submitted = parseSubmittedCase(
      {
        data: {
          submitCase: {
            id: previous.id,
            status: 'SUBMITTED',
            submittedAt: '2026-01-03T00:00:00Z',
            updatedAt: '2026-01-03T00:00:00Z',
          },
        },
      },
      previous,
    );
    expect(submitted.canEdit).toBe(false);
    expect(submitted.status).toBe('SUBMITTED');
    expect(submitted.submittedAtLabel).not.toBeNull();
  });
});
