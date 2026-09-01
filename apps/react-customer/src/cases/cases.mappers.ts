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
  ALLOWED_DOCUMENT_CONTENT_TYPES,
  CASE_FORM_FIELD_KEYS,
  CREATE_DRAFT_TITLE_MAX_LENGTH,
  MAX_DOCUMENT_BYTES,
  OPTIONAL_COMPANY_FIELD_KEY,
  type CaseDocument,
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
  DocumentUploadError,
  type DraftFormFieldErrors,
  type DraftFormModel,
  type GraphqlCaseDetailBody,
  type GraphqlCasesBody,
  type GraphqlCreateDraftBody,
  type GraphqlDocumentWire,
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
  const documents: readonly CaseDocument[] = parseCaseDocuments(
    body.data?.case?.documents,
  );
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
    documents,
    canEdit: raw.status === 'DRAFT',
    canUpload: canUploadDocuments(raw.status),
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

/** Pure: map updateDraftCase body → detail DTO (keeps prior documents). */
export function parseUpdatedDraft(
  body: GraphqlUpdateDraftBody | GraphqlResponse<GraphqlUpdateDraftBody['data']>,
  previous: CaseDraftDetail,
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
    documents: previous.documents,
    canEdit: raw.status === 'DRAFT',
    canUpload: canUploadDocuments(raw.status),
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
    canUpload: canUploadDocuments(raw.status),
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

/** Pure: Draft or Submitted may receive uploads (KYC-040). */
export function canUploadDocuments(status: CaseStatus): boolean {
  return status === 'DRAFT' || status === 'SUBMITTED';
}

/** Pure: human-readable file size (mirror Angular admin). */
export function formatByteSize(sizeBytes: number): string {
  if (sizeBytes < 1024) {
    return `${String(sizeBytes)} B`;
  }
  if (sizeBytes < 1024 * 1024) {
    return `${(sizeBytes / 1024).toFixed(1)} KB`;
  }
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** Pure: REST upload path (relative to apiBaseUrl). */
export function toDocumentUploadPath(caseId: string): string {
  return `api/cases/${encodeURIComponent(caseId)}/documents`;
}

/** Pure: normalize browser MIME for allow-list checks. */
export function normalizeDocumentContentType(contentType: string): string | null {
  const type: string = contentType.split(';', 2)[0]?.trim().toLowerCase() ?? '';
  if (type === 'image/jpg') {
    return 'image/jpeg';
  }
  if ((ALLOWED_DOCUMENT_CONTENT_TYPES as readonly string[]).includes(type)) {
    return type;
  }
  return null;
}

/** Pure: infer allow-listed MIME from filename when browser omits type. */
export function contentTypeFromFileName(fileName: string): string | null {
  const lower: string = fileName.trim().toLowerCase();
  if (lower.endsWith('.pdf')) {
    return 'application/pdf';
  }
  if (lower.endsWith('.png')) {
    return 'image/png';
  }
  if (lower.endsWith('.jpg') || lower.endsWith('.jpeg')) {
    return 'image/jpeg';
  }
  return null;
}

/** Pure: client-side type/size gate before POST (mirrors API messages). */
export function validateDocumentFile(file: File): string | null {
  if (file.size <= 0) {
    return CASES_DRAFT_MESSAGES.docsEmptyFile;
  }
  if (file.size > MAX_DOCUMENT_BYTES) {
    return CASES_DRAFT_MESSAGES.docsSizeRejected;
  }
  if (normalizeDocumentContentType(file.type) !== null) {
    return null;
  }
  // Some browsers leave `File.type` empty — fall back to extension only then.
  if (!file.type.trim() && contentTypeFromFileName(file.name) !== null) {
    return null;
  }
  return CASES_DRAFT_MESSAGES.docsTypeRejected;
}

/** Pure: parse GraphQL documents array → CaseDocument[]. */
export function parseCaseDocuments(
  rawDocs: Array<GraphqlDocumentWire | null> | null | undefined,
): readonly CaseDocument[] {
  if (!rawDocs) {
    return [];
  }
  return rawDocs
    .filter((d): d is GraphqlDocumentWire => d != null)
    .map((d): CaseDocument => parseCaseDocument(d));
}

/** Pure: map REST 201 upload JSON → CaseDocument. */
export function parseUploadedDocument(raw: unknown): CaseDocument {
  if (raw === null || typeof raw !== 'object' || Array.isArray(raw)) {
    throw new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadIncomplete);
  }
  try {
    return parseCaseDocument(raw as GraphqlDocumentWire);
  } catch (err: unknown) {
    if (err instanceof CasesLoadError) {
      throw new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadIncomplete);
    }
    throw err;
  }
}

/** Pure: prepend uploaded doc (API lists newest first). */
export function prependDocument(
  documents: readonly CaseDocument[],
  uploaded: CaseDocument,
): readonly CaseDocument[] {
  return [uploaded, ...documents.filter((d: CaseDocument): boolean => d.id !== uploaded.id)];
}

/** Pure: map REST upload errors → DocumentUploadError. */
export async function toDocumentUploadError(response: Response): Promise<DocumentUploadError> {
  let payload: unknown = null;
  try {
    payload = await response.json();
  } catch {
    payload = null;
  }

  const record: Record<string, unknown> | null =
    payload !== null && typeof payload === 'object' && !Array.isArray(payload)
      ? (payload as Record<string, unknown>)
      : null;
  const code: string | undefined =
    record !== null && typeof record['code'] === 'string' ? record['code'] : undefined;

  if (code === 'AUTH_NOT_AUTHORIZED') {
    return new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadUnauthorized, code);
  }
  if (code === 'NOT_FOUND') {
    return new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadNotFound, code);
  }
  if (code === 'DOMAIN') {
    const message: string =
      typeof record?.['error'] === 'string' && record['error'].trim()
        ? record['error'].trim()
        : CASES_DRAFT_MESSAGES.docsUploadDomain;
    return new DocumentUploadError(message, code);
  }
  if (code === 'STORAGE') {
    return new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadStorage, code);
  }
  if (code === 'VALIDATION') {
    const errorsRaw: unknown = record?.['errors'];
    if (Array.isArray(errorsRaw)) {
      const first: unknown = errorsRaw[0];
      if (typeof first === 'string' && first.trim()) {
        return new DocumentUploadError(first.trim(), code);
      }
    }
  }

  return new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadFailed, code);
}

/** Pure: map transport / unknown upload errors. */
export function toDocumentUploadTransportError(err: unknown): DocumentUploadError {
  if (err instanceof DocumentUploadError) {
    return err;
  }
  if (err instanceof TypeError) {
    return new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadNetworkFailed, 'NETWORK');
  }
  if (err instanceof Error && /Failed to fetch|NetworkError/i.test(err.message)) {
    return new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadNetworkFailed, 'NETWORK');
  }
  return new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadFailed);
}

function parseCaseDocument(d: GraphqlDocumentWire): CaseDocument {
  if (
    !d.id ||
    !d.fileName ||
    !d.contentType ||
    d.sizeBytes === undefined ||
    d.sizeBytes === null ||
    !d.uploadedAt ||
    !d.uploadedBy
  ) {
    throw new CasesLoadError(CASES_DRAFT_MESSAGES.docsIncomplete);
  }
  return {
    id: d.id,
    fileName: d.fileName,
    contentType: d.contentType,
    sizeBytes: d.sizeBytes,
    sizeLabel: formatByteSize(d.sizeBytes),
    uploadedAt: d.uploadedAt,
    uploadedAtLabel: formatUpdatedAt(d.uploadedAt),
    uploadedBy: d.uploadedBy,
  };
}

/** Exported for tests / labels — person field keys. */
export type { CaseFormFieldKey };
