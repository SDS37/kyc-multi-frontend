import { describe, expect, it } from 'vitest';
import {
  draftFormToFormDataJson,
  emptyDraftForm,
  formatByteSize,
  hasDraftFieldErrors,
  isCaseId,
  parseCaseDraftDetail,
  parseCasesPage,
  parseCreatedDraft,
  parseFormDataToDraftForm,
  parseStatusFilterValue,
  parseSubmittedCase,
  parseUpdatedDraft,
  prependDocument,
  toCreateDraftVariables,
  toDocumentUploadPath,
  toListCasesVariables,
  toUpdateDraftVariables,
  validateCreateDraftTitle,
  validateDocumentFile,
  validateDraftSave,
  validateDraftSubmit,
} from './cases.mappers';
import { CASES_DRAFT_MESSAGES } from './cases.messages';
import {
  DraftActionError,
  CasesLoadError,
  CreateDraftError,
  MAX_DOCUMENT_BYTES,
} from './cases.models';

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

  it('parseCaseDraftDetail maps DRAFT as editable with documents', (): void => {
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
          documents: [
            {
              id: 'dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb',
              fileName: 'id.pdf',
              contentType: 'application/pdf',
              sizeBytes: 2048,
              uploadedAt: '2026-01-02T04:00:00Z',
              uploadedBy: 'cccccccc-dddd-eeee-ffff-aaaaaaaaaaaa',
            },
          ],
        },
      },
    });
    expect(detail.canEdit).toBe(true);
    expect(detail.canUpload).toBe(true);
    expect(detail.form.fullName).toBe('Ada');
    expect(detail.submittedAtLabel).toBeNull();
    expect(detail.documents).toHaveLength(1);
    expect(detail.documents[0]?.fileName).toBe('id.pdf');
    expect(detail.documents[0]?.sizeLabel).toBe('2.0 KB');
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
          documents: [],
        },
      },
    });

    expect(() =>
      parseUpdatedDraft(
        {
          errors: [{ message: 'Locked', extensions: { code: 'DOMAIN' } }],
        },
        previous,
      ),
    ).toThrow(DraftActionError);

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
    expect(submitted.canUpload).toBe(true);
    expect(submitted.status).toBe('SUBMITTED');
    expect(submitted.submittedAtLabel).not.toBeNull();
  });

  it('validateDocumentFile rejects empty, oversized, and wrong types', (): void => {
    expect(validateDocumentFile(new File([], 'empty.pdf', { type: 'application/pdf' }))).toBe(
      CASES_DRAFT_MESSAGES.docsEmptyFile,
    );
    const big = new File([new Uint8Array(MAX_DOCUMENT_BYTES + 1)], 'big.pdf', {
      type: 'application/pdf',
    });
    expect(validateDocumentFile(big)).toBe(CASES_DRAFT_MESSAGES.docsSizeRejected);
    expect(
      validateDocumentFile(new File([new Uint8Array(10)], 'x.txt', { type: 'text/plain' })),
    ).toBe(CASES_DRAFT_MESSAGES.docsTypeRejected);
    expect(
      validateDocumentFile(new File([new Uint8Array(10)], 'ok.jpg', { type: 'image/jpg' })),
    ).toBeNull();
    expect(
      validateDocumentFile(new File([new Uint8Array(10)], 'ok.pdf', { type: '' })),
    ).toBeNull();
    expect(
      validateDocumentFile(new File([new Uint8Array(10)], 'bad.bin', { type: '' })),
    ).toBe(CASES_DRAFT_MESSAGES.docsTypeRejected);
  });

  it('formatByteSize and prependDocument helpers', (): void => {
    expect(formatByteSize(500)).toBe('500 B');
    expect(toDocumentUploadPath('aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee')).toBe(
      'api/cases/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/documents',
    );
    const a = {
      id: '1',
      fileName: 'a.pdf',
      contentType: 'application/pdf',
      sizeBytes: 1,
      sizeLabel: '1 B',
      uploadedAt: '2026-01-01T00:00:00Z',
      uploadedAtLabel: 'x',
      uploadedBy: 'u',
    };
    const b = { ...a, id: '2', fileName: 'b.pdf' };
    expect(prependDocument([a], b).map((d) => d.id)).toEqual(['2', '1']);
  });
});
