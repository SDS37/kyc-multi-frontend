import type { GraphqlError, GraphqlResponse } from '../shared/graphql.models';
import {
  CASE_FORM_FIELD_LABELS,
  CASE_STATUS_LABELS,
  CASES_DRAFT_MESSAGES,
  CASES_LIST_MESSAGES,
  caseStatusLabel,
  draftFieldRequiredMessage,
  openCaseAriaLabel,
  unexpectedCaseStatusMessage,
} from './cases.messages';
import {
  CASE_FORM_FIELD_KEYS,
  CREATE_DRAFT_TITLE_MAX_LENGTH,
  OPTIONAL_COMPANY_FIELD_KEY,
  type CaseDraftDetail,
  type CaseFormFieldKey,
  type CaseListItem,
  type CaseListPage,
  type CaseStatus,
  CasesLoadError,
  type CreateDraftCaseInput,
  CreateDraftError,
  type CreatedDraftCase,
  DraftActionError,
  type DraftFormFieldErrors,
  type DraftFormModel,
  type GraphqlCaseDetailBody,
  type GraphqlCasesBody,
  type GraphqlCreateDraftBody,
  type GraphqlSubmitCaseBody,
  type GraphqlUpdateDraftBody,
  type ListCasesParams,
  type ListCasesVariables,
  type SubmitCaseInput,
  type UpdateDraftCaseInput,
  isCaseStatus,
} from './cases.models';

export { caseStatusLabel, CASE_STATUS_LABELS, CASE_FORM_FIELD_LABELS };

const UPDATED_AT_FORMATTER: Intl.DateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
});

const ISO_DATE_PATTERN: RegExp = /^\d{4}-\d{2}-\d{2}$/;

/** Pure: loose UUID check for route params (API uses UUID!). */
export function isCaseId(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    value,
  );
}

/** Pure: build GraphQL variables from list params. */
export function toListCasesVariables(params: ListCasesParams = {}): ListCasesVariables {
  return {
    status: params.status ?? null,
    skip: params.skip ?? 0,
    take: params.take ?? 20,
  };
}

/** Pure: map GraphQL `cases` body → page DTO (throws CasesLoadError on bad payload). */
export function parseCasesPage(
  body: GraphqlCasesBody | GraphqlResponse<GraphqlCasesBody['data']>,
  requested: ListCasesVariables,
): CaseListPage {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw new CasesLoadError(
      gqlError.message?.trim() || CASES_LIST_MESSAGES.listLoadFailed,
      gqlError.extensions?.code,
    );
  }

  const page = body.data?.cases;
  if (!page || !Array.isArray(page.items)) {
    throw new CasesLoadError(CASES_LIST_MESSAGES.listLoadFailed);
  }

  const items: CaseListItem[] = page.items.map((raw): CaseListItem => {
    if (!raw?.id || !raw.title || !raw.updatedAt || !raw.status) {
      throw new CasesLoadError(CASES_LIST_MESSAGES.listIncomplete);
    }
    if (!isCaseStatus(raw.status)) {
      throw new CasesLoadError(unexpectedCaseStatusMessage(raw.status));
    }
    return {
      id: raw.id,
      title: raw.title,
      status: raw.status,
      statusLabel: caseStatusLabel(raw.status),
      updatedAt: raw.updatedAt,
      updatedAtLabel: formatUpdatedAt(raw.updatedAt),
      openAriaLabel: openCaseAriaLabel(raw.title),
    };
  });

  return {
    items,
    totalCount: page.totalCount ?? items.length,
    skip: page.skip ?? requested.skip,
    take: page.take ?? requested.take,
  };
}

/** Pure: normalize unknown filter control values. */
export function parseStatusFilterValue(value: unknown): CaseStatus | null | undefined {
  if (value === null || value === '') {
    return null;
  }
  if (typeof value === 'string' && isCaseStatus(value)) {
    return value;
  }
  return undefined;
}

/** Pure: map transport / unknown errors to CasesLoadError. */
export function toCasesLoadError(err: unknown): CasesLoadError {
  if (err instanceof CasesLoadError) {
    return err;
  }
  if (err instanceof TypeError) {
    return new CasesLoadError(CASES_LIST_MESSAGES.listNetworkFailed, 'NETWORK');
  }
  if (err instanceof Error && /GraphQL HTTP|Failed to fetch|NetworkError/i.test(err.message)) {
    return new CasesLoadError(CASES_LIST_MESSAGES.listNetworkFailed, 'NETWORK');
  }
  return new CasesLoadError(CASES_LIST_MESSAGES.listLoadFailed);
}

/** Pure: map detail load errors (NOT_FOUND gets clearer copy). */
export function toCaseDetailLoadError(err: unknown): CasesLoadError {
  if (err instanceof CasesLoadError) {
    if (err.code === 'NOT_FOUND') {
      return new CasesLoadError(CASES_DRAFT_MESSAGES.loadNotFound, err.code);
    }
    return err;
  }
  if (err instanceof TypeError) {
    return new CasesLoadError(CASES_DRAFT_MESSAGES.loadNetworkFailed, 'NETWORK');
  }
  if (err instanceof Error && /GraphQL HTTP|Failed to fetch|NetworkError/i.test(err.message)) {
    return new CasesLoadError(CASES_DRAFT_MESSAGES.loadNetworkFailed, 'NETWORK');
  }
  return new CasesLoadError(CASES_DRAFT_MESSAGES.loadFailed);
}

/** Pure: trim + validate create title (mirrors API rules). */
export function normalizeCreateDraftTitle(title: string): string {
  return title.trim();
}

export function validateCreateDraftTitle(title: string): string | null {
  const normalized: string = normalizeCreateDraftTitle(title);
  if (!normalized) {
    return CASES_LIST_MESSAGES.createTitleRequired;
  }
  if (normalized.length > CREATE_DRAFT_TITLE_MAX_LENGTH) {
    return CASES_LIST_MESSAGES.createTitleMaxLength;
  }
  return null;
}

/** Pure: map createDraftCase body → DTO. */
export function parseCreatedDraft(
  body: GraphqlCreateDraftBody | GraphqlResponse<GraphqlCreateDraftBody['data']>,
): CreatedDraftCase {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    const code: string | undefined = gqlError.extensions?.code;
    if (code === 'AUTH_NOT_AUTHORIZED') {
      throw new CreateDraftError(CASES_LIST_MESSAGES.createUnauthorized, code);
    }
    throw new CreateDraftError(
      gqlError.message?.trim() || CASES_LIST_MESSAGES.createFailed,
      code,
    );
  }

  const created = body.data?.createDraftCase;
  if (!created?.id || !created.title || !created.status || !created.updatedAt) {
    throw new CreateDraftError(CASES_LIST_MESSAGES.createIncomplete);
  }
  if (!isCaseStatus(created.status)) {
    throw new CreateDraftError(unexpectedCaseStatusMessage(created.status));
  }

  return {
    id: created.id,
    title: created.title,
    status: created.status,
    updatedAt: created.updatedAt,
  };
}

/** Pure: map transport / unknown errors to CreateDraftError. */
export function toCreateDraftError(err: unknown): CreateDraftError {
  if (err instanceof CreateDraftError) {
    return err;
  }
  if (err instanceof TypeError) {
    return new CreateDraftError(CASES_LIST_MESSAGES.createNetworkFailed, 'NETWORK');
  }
  if (err instanceof Error && /GraphQL HTTP|Failed to fetch|NetworkError/i.test(err.message)) {
    return new CreateDraftError(CASES_LIST_MESSAGES.createNetworkFailed, 'NETWORK');
  }
  return new CreateDraftError(CASES_LIST_MESSAGES.createFailed);
}

/** Pure: build create mutation input (never includes tenant/user ids). */
export function toCreateDraftVariables(input: CreateDraftCaseInput): {
  input: { title: string };
} {
  return {
    input: {
      title: normalizeCreateDraftTitle(input.title),
    },
  };
}

/** Pure: empty draft form. */
export function emptyDraftForm(title: string = ''): DraftFormModel {
  return {
    title,
    fullName: '',
    dateOfBirth: '',
    nationality: '',
    address: '',
    companyName: '',
  };
}

/** Pure: parse FormData JSON into draft form fields. */
export function parseFormDataToDraftForm(
  title: string,
  formDataRaw: string,
): DraftFormModel {
  const base: DraftFormModel = emptyDraftForm(title);
  const trimmed: string = formDataRaw.trim();
  if (!trimmed) {
    return base;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed) as unknown;
  } catch {
    return base;
  }

  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return base;
  }

  const record: Record<string, unknown> = parsed as Record<string, unknown>;
  return {
    title,
    fullName: readFormString(record, 'fullName'),
    dateOfBirth: readFormString(record, 'dateOfBirth'),
    nationality: readFormString(record, 'nationality'),
    address: readFormString(record, 'address'),
    companyName: readFormString(record, OPTIONAL_COMPANY_FIELD_KEY),
  };
}

/** Pure: serialize draft form → FormData JSON string for the API. */
export function draftFormToFormDataJson(form: DraftFormModel): string {
  const payload: Record<string, string> = {};
  for (const key of CASE_FORM_FIELD_KEYS) {
    const value: string = form[key].trim();
    if (value) {
      payload[key] = value;
    }
  }
  const company: string = form.companyName.trim();
  if (company) {
    payload[OPTIONAL_COMPANY_FIELD_KEY] = company;
  }
  return JSON.stringify(payload);
}

/** Pure: client validation before save (title only — FormData optional on update). */
export function validateDraftSave(form: DraftFormModel): DraftFormFieldErrors {
  const errors: {
    title?: string;
    fullName?: string;
    dateOfBirth?: string;
    nationality?: string;
    address?: string;
  } = {};

  const titleError: string | null = validateCreateDraftTitle(form.title);
  if (titleError !== null) {
    errors.title = titleError;
  }

  const dob: string = form.dateOfBirth.trim();
  if (dob && !isIsoDate(dob)) {
    errors.dateOfBirth = CASES_DRAFT_MESSAGES.dateOfBirthInvalid;
  }

  return errors;
}

/** Pure: client validation before submit (required person fields). */
export function validateDraftSubmit(form: DraftFormModel): DraftFormFieldErrors {
  const errors: {
    title?: string;
    fullName?: string;
    dateOfBirth?: string;
    nationality?: string;
    address?: string;
  } = { ...validateDraftSave(form) };

  for (const key of CASE_FORM_FIELD_KEYS) {
    if (!form[key].trim()) {
      errors[key] = draftFieldRequiredMessage(key);
    }
  }

  const dob: string = form.dateOfBirth.trim();
  if (dob && !isIsoDate(dob)) {
    errors.dateOfBirth = CASES_DRAFT_MESSAGES.dateOfBirthInvalid;
  }

  return errors;
}

export function hasDraftFieldErrors(errors: DraftFormFieldErrors): boolean {
  return (
    errors.title !== undefined ||
    errors.fullName !== undefined ||
    errors.dateOfBirth !== undefined ||
    errors.nationality !== undefined ||
    errors.address !== undefined
  );
}

/** Pure: map GraphQL `case` detail → draft DTO. */
export function parseCaseDraftDetail(
  body: GraphqlCaseDetailBody | GraphqlResponse<GraphqlCaseDetailBody['data']>,
): CaseDraftDetail {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    const code: string | undefined = gqlError.extensions?.code;
    if (code === 'NOT_FOUND') {
      throw new CasesLoadError(CASES_DRAFT_MESSAGES.loadNotFound, code);
    }
    throw new CasesLoadError(
      gqlError.message?.trim() || CASES_DRAFT_MESSAGES.loadFailed,
      code,
    );
  }

  const raw = body.data?.case?.case;
  if (
    !raw?.id ||
    !raw.title ||
    !raw.status ||
    raw.formData === undefined ||
    !raw.updatedAt
  ) {
    throw new CasesLoadError(CASES_DRAFT_MESSAGES.loadIncomplete);
  }
  if (!isCaseStatus(raw.status)) {
    throw new CasesLoadError(unexpectedCaseStatusMessage(raw.status));
  }

  const formDataRaw: string = raw.formData ?? '{}';
  const submittedAt: string | null = raw.submittedAt ?? null;
  return {
    id: raw.id,
    title: raw.title,
    status: raw.status,
    statusLabel: caseStatusLabel(raw.status),
    formDataRaw,
    form: parseFormDataToDraftForm(raw.title, formDataRaw),
    updatedAt: raw.updatedAt,
    updatedAtLabel: formatUpdatedAt(raw.updatedAt),
    submittedAt,
    submittedAtLabel: submittedAt !== null ? formatUpdatedAt(submittedAt) : null,
    canEdit: raw.status === 'DRAFT',
  };
}

/** Pure: build updateDraftCase variables (never tenant/user ids). */
export function toUpdateDraftVariables(input: UpdateDraftCaseInput): {
  input: { id: string; title: string; formData: string };
} {
  return {
    input: {
      id: input.id,
      title: normalizeCreateDraftTitle(input.title),
      formData: input.formData,
    },
  };
}

/** Pure: map updateDraftCase body → detail DTO. */
export function parseUpdatedDraft(
  body: GraphqlUpdateDraftBody | GraphqlResponse<GraphqlUpdateDraftBody['data']>,
): CaseDraftDetail {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw mapDraftActionGqlError(gqlError, 'save');
  }

  const raw = body.data?.updateDraftCase;
  if (
    !raw?.id ||
    !raw.title ||
    !raw.status ||
    raw.formData === undefined ||
    !raw.updatedAt
  ) {
    throw new DraftActionError(CASES_DRAFT_MESSAGES.saveIncomplete);
  }
  if (!isCaseStatus(raw.status)) {
    throw new DraftActionError(unexpectedCaseStatusMessage(raw.status));
  }

  const formDataRaw: string = raw.formData ?? '{}';
  const submittedAt: string | null = raw.submittedAt ?? null;
  return {
    id: raw.id,
    title: raw.title,
    status: raw.status,
    statusLabel: caseStatusLabel(raw.status),
    formDataRaw,
    form: parseFormDataToDraftForm(raw.title, formDataRaw),
    updatedAt: raw.updatedAt,
    updatedAtLabel: formatUpdatedAt(raw.updatedAt),
    submittedAt,
    submittedAtLabel: submittedAt !== null ? formatUpdatedAt(submittedAt) : null,
    canEdit: raw.status === 'DRAFT',
  };
}

/** Pure: build submitCase variables. */
export function toSubmitCaseVariables(input: SubmitCaseInput): {
  input: { id: string };
} {
  return { input: { id: input.id } };
}

/** Pure: map submitCase body → detail patch fields. */
export function parseSubmittedCase(
  body: GraphqlSubmitCaseBody | GraphqlResponse<GraphqlSubmitCaseBody['data']>,
  previous: CaseDraftDetail,
): CaseDraftDetail {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw mapDraftActionGqlError(gqlError, 'submit');
  }

  const raw = body.data?.submitCase;
  if (!raw?.id || !raw.status || !raw.updatedAt) {
    throw new DraftActionError(CASES_DRAFT_MESSAGES.submitIncomplete);
  }
  if (!isCaseStatus(raw.status)) {
    throw new DraftActionError(unexpectedCaseStatusMessage(raw.status));
  }

  const submittedAt: string | null = raw.submittedAt ?? null;
  return {
    ...previous,
    id: raw.id,
    status: raw.status,
    statusLabel: caseStatusLabel(raw.status),
    updatedAt: raw.updatedAt,
    updatedAtLabel: formatUpdatedAt(raw.updatedAt),
    submittedAt,
    submittedAtLabel: submittedAt !== null ? formatUpdatedAt(submittedAt) : null,
    canEdit: false,
  };
}

/** Pure: map transport / GraphQL action errors. */
export function toDraftActionError(
  err: unknown,
  action: 'save' | 'submit',
): DraftActionError {
  if (err instanceof DraftActionError) {
    return err;
  }
  const networkMessage: string =
    action === 'save'
      ? CASES_DRAFT_MESSAGES.saveNetworkFailed
      : CASES_DRAFT_MESSAGES.submitNetworkFailed;
  const fallback: string =
    action === 'save' ? CASES_DRAFT_MESSAGES.saveFailed : CASES_DRAFT_MESSAGES.submitFailed;

  if (err instanceof TypeError) {
    return new DraftActionError(networkMessage, 'NETWORK');
  }
  if (err instanceof Error && /GraphQL HTTP|Failed to fetch|NetworkError/i.test(err.message)) {
    return new DraftActionError(networkMessage, 'NETWORK');
  }
  return new DraftActionError(fallback);
}

function mapDraftActionGqlError(
  gqlError: GraphqlError,
  action: 'save' | 'submit',
): DraftActionError {
  const code: string | undefined = gqlError.extensions?.code;
  if (code === 'AUTH_NOT_AUTHORIZED') {
    return new DraftActionError(CASES_DRAFT_MESSAGES.actionUnauthorized, code);
  }
  if (code === 'NOT_FOUND') {
    return new DraftActionError(CASES_DRAFT_MESSAGES.actionNotFound, code);
  }
  if (code === 'DOMAIN') {
    return new DraftActionError(
      gqlError.message?.trim() || CASES_DRAFT_MESSAGES.actionDomain,
      code,
    );
  }
  const fallback: string =
    action === 'save' ? CASES_DRAFT_MESSAGES.saveFailed : CASES_DRAFT_MESSAGES.submitFailed;
  return new DraftActionError(gqlError.message?.trim() || fallback, code);
}

function readFormString(record: Record<string, unknown>, key: string): string {
  const value: unknown = record[key];
  return typeof value === 'string' ? value : '';
}

function isIsoDate(value: string): boolean {
  if (!ISO_DATE_PATTERN.test(value)) {
    return false;
  }
  const [yearRaw, monthRaw, dayRaw] = value.split('-');
  const year: number = Number(yearRaw);
  const month: number = Number(monthRaw);
  const day: number = Number(dayRaw);
  const date: Date = new Date(Date.UTC(year, month - 1, day));
  return (
    date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day
  );
}

function formatUpdatedAt(iso: string): string {
  const date: Date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return UPDATED_AT_FORMATTER.format(date);
}

/** Exported for tests / labels — person field keys. */
export type { CaseFormFieldKey };
