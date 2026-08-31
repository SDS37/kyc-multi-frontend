import { HttpErrorResponse } from '@angular/common/http';
import { GraphqlError } from '../shared/graphql.models';
import {
  CASE_FORM_FIELD_LABELS,
  CASES_REVIEW_MESSAGES,
  caseStatusLabel,
  downloadDocumentAriaLabel,
  openCaseAriaLabel,
  rejectCommentMaxLengthMessage,
  unexpectedCaseStatusMessage,
} from './cases.messages';
import {
  CASE_FORM_FIELD_KEYS,
  CaseActionError,
  CaseComment,
  CaseDetail,
  CaseDocument,
  CaseDownloadError,
  CaseFormField,
  CaseFormFieldKey,
  CaseListItem,
  CaseListPage,
  CaseReviewActions,
  CaseStatus,
  CasesLoadError,
  GraphqlCaseActionBody,
  GraphqlCaseDetailBody,
  GraphqlCasesBody,
  ListCasesParams,
  REVIEW_COMMENT_MAX_LENGTH,
  isCaseStatus,
} from './cases.models';

export interface ListCasesVariables {
  status: CaseStatus | null;
  skip: number;
  take: number;
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
  body: GraphqlCasesBody,
  requested: ListCasesVariables,
): CaseListPage {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw new CasesLoadError(
      gqlError.message?.trim() || CASES_REVIEW_MESSAGES.listLoadFailed,
      gqlError.extensions?.code,
    );
  }

  const page = body.data?.cases;
  if (!page || !Array.isArray(page.items)) {
    throw new CasesLoadError(CASES_REVIEW_MESSAGES.listLoadFailed);
  }

  const items: CaseListItem[] = page.items.map(
    (raw): CaseListItem => {
      if (!raw?.id || !raw.title || !raw.customerEmail || !raw.updatedAt || !raw.status) {
        throw new CasesLoadError(CASES_REVIEW_MESSAGES.listIncomplete);
      }
      if (!isCaseStatus(raw.status)) {
        throw new CasesLoadError(unexpectedCaseStatusMessage(raw.status));
      }
      return {
        id: raw.id,
        title: raw.title,
        status: raw.status,
        statusLabel: caseStatusLabel(raw.status),
        customerEmail: raw.customerEmail,
        updatedAt: raw.updatedAt,
        openAriaLabel: openCaseAriaLabel(raw.title),
      };
    },
  );

  return {
    items,
    totalCount: page.totalCount ?? items.length,
    skip: page.skip ?? requested.skip,
    take: page.take ?? requested.take,
  };
}

/** Pure: normalize unknown filter control values. */
export function parseStatusFilterValue(value: unknown): CaseStatus | null | undefined {
  if (value === null) {
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
  if (err instanceof HttpErrorResponse) {
    return new CasesLoadError(CASES_REVIEW_MESSAGES.listNetworkFailed, 'NETWORK');
  }
  return new CasesLoadError(CASES_REVIEW_MESSAGES.listLoadFailed);
}

/** Pure: which review buttons apply for a case status (API DOMAIN rules). */
export function resolveReviewActions(status: CaseStatus): CaseReviewActions {
  return {
    canStartReview: status === 'SUBMITTED',
    canApprove: status === 'IN_REVIEW',
    canReject: status === 'IN_REVIEW',
  };
}

/** Pure: parse FormData JSON string into display fields (known keys first). */
export function parseCaseFormData(formDataRaw: string): readonly CaseFormField[] {
  const trimmed: string = formDataRaw.trim();
  if (!trimmed) {
    return [];
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed) as unknown;
  } catch {
    return [{ key: 'formData', label: CASES_REVIEW_MESSAGES.formDataFallbackLabel, value: trimmed }];
  }

  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return [{ key: 'formData', label: CASES_REVIEW_MESSAGES.formDataFallbackLabel, value: trimmed }];
  }

  const record: Record<string, unknown> = parsed as Record<string, unknown>;

  const knownFields: CaseFormField[] = CASE_FORM_FIELD_KEYS.filter(
    (key): boolean => key in record,
  ).map(
    (key): CaseFormField => ({
      key,
      label: CASE_FORM_FIELD_LABELS[key],
      value: formatFormFieldValue(record[key]),
    }),
  );

  const knownKeys: ReadonlySet<string> = new Set<string>(
    knownFields.map((field: CaseFormField): string => field.key),
  );

  const extraFields: CaseFormField[] = [...Object.keys(record)]
    .sort()
    .filter((key): boolean => !knownKeys.has(key))
    .map(
      (key): CaseFormField => ({
        key,
        label: humanizeFormKey(key),
        value: formatFormFieldValue(record[key]),
      }),
    );

  return [...knownFields, ...extraFields];
}

/** Pure: map GraphQL `case(id)` body → CaseDetail. */
export function parseCaseDetail(body: GraphqlCaseDetailBody): CaseDetail {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw new CasesLoadError(
      gqlError.message?.trim() || CASES_REVIEW_MESSAGES.loadFailed,
      gqlError.extensions?.code,
    );
  }

  const envelope = body.data?.case;
  const raw = envelope?.case;
  if (!raw?.id || !raw.title || !raw.status || !raw.customerEmail || !raw.customerUserId) {
    throw new CasesLoadError(CASES_REVIEW_MESSAGES.loadIncomplete);
  }
  if (!raw.createdAt || !raw.updatedAt || raw.formData === undefined || raw.formData === null) {
    throw new CasesLoadError(CASES_REVIEW_MESSAGES.loadIncomplete);
  }
  if (!isCaseStatus(raw.status)) {
    throw new CasesLoadError(unexpectedCaseStatusMessage(raw.status));
  }

  const comments: CaseComment[] = (envelope?.comments ?? [])
    .filter((c): c is NonNullable<typeof c> => c != null)
    .map((c): CaseComment => {
      if (!c.text || !c.createdAt || !c.authorUserId) {
        throw new CasesLoadError(CASES_REVIEW_MESSAGES.commentsIncomplete);
      }
      return {
        text: c.text,
        createdAt: c.createdAt,
        authorUserId: c.authorUserId,
      };
    });

  const documents: CaseDocument[] = (envelope?.documents ?? [])
    .filter((d): d is NonNullable<typeof d> => d != null)
    .map((d): CaseDocument => {
      if (
        !d.id ||
        !d.fileName ||
        !d.contentType ||
        d.sizeBytes === undefined ||
        d.sizeBytes === null ||
        !d.uploadedAt ||
        !d.uploadedBy
      ) {
        throw new CasesLoadError(CASES_REVIEW_MESSAGES.documentsIncomplete);
      }
      return {
        id: d.id,
        fileName: d.fileName,
        contentType: d.contentType,
        sizeBytes: d.sizeBytes,
        sizeLabel: formatByteSize(d.sizeBytes),
        uploadedAt: d.uploadedAt,
        uploadedBy: d.uploadedBy,
        downloadAriaLabel: downloadDocumentAriaLabel(d.fileName),
      };
    });

  const formDataRaw: string = raw.formData;

  return {
    id: raw.id,
    title: raw.title,
    status: raw.status,
    customerEmail: raw.customerEmail,
    customerUserId: raw.customerUserId,
    formDataRaw,
    formFields: parseCaseFormData(formDataRaw),
    reviewComment: raw.reviewComment ?? null,
    reviewedAt: raw.reviewedAt ?? null,
    reviewedBy: raw.reviewedBy ?? null,
    submittedAt: raw.submittedAt ?? null,
    createdAt: raw.createdAt,
    updatedAt: raw.updatedAt,
    comments,
    documents,
  };
}

/** Pure: REST download path under apiBaseUrl. */
export function toDocumentDownloadUrl(
  apiBaseUrl: string,
  caseId: string,
  documentId: string,
): string {
  const base: string = apiBaseUrl.replace(/\/$/, '');
  return `${base}/api/cases/${encodeURIComponent(caseId)}/documents/${encodeURIComponent(documentId)}`;
}

/** Pure: human-readable file size. */
export function formatByteSize(sizeBytes: number): string {
  if (sizeBytes < 1024) {
    return `${sizeBytes} B`;
  }
  if (sizeBytes < 1024 * 1024) {
    return `${(sizeBytes / 1024).toFixed(1)} KB`;
  }
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
}

/**
 * Pure: normalize reject comment for the API.
 * Returns `{ ok: true, comment }` or `{ ok: false, message }`.
 */
export function normalizeRejectComment(
  raw: string,
): { ok: true; comment: string } | { ok: false; message: string } {
  const comment: string = raw.trim();
  if (!comment) {
    return { ok: false, message: CASES_REVIEW_MESSAGES.rejectCommentRequiredAction };
  }
  if (comment.length > REVIEW_COMMENT_MAX_LENGTH) {
    return {
      ok: false,
      message: rejectCommentMaxLengthMessage(),
    };
  }
  return { ok: true, comment };
}

/** Pure: optional approve comment (empty → null). */
export function normalizeOptionalReviewComment(
  raw: string,
): { ok: true; comment: string | null } | { ok: false; message: string } {
  const comment: string = raw.trim();
  if (!comment) {
    return { ok: true, comment: null };
  }
  if (comment.length > REVIEW_COMMENT_MAX_LENGTH) {
    return {
      ok: false,
      message: rejectCommentMaxLengthMessage(),
    };
  }
  return { ok: true, comment };
}

type CaseActionKind = 'startCaseReview' | 'approveCase' | 'rejectCase';

/** Pure: parse mutation body → new status. */
export function parseCaseActionStatus(
  body: GraphqlCaseActionBody,
  kind: CaseActionKind,
): CaseStatus {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw new CaseActionError(
      gqlError.message?.trim() || CASES_REVIEW_MESSAGES.actionFailed,
      gqlError.extensions?.code,
    );
  }

  const payload = body.data?.[kind];
  if (!payload?.status || !isCaseStatus(payload.status)) {
    throw new CaseActionError(CASES_REVIEW_MESSAGES.actionIncomplete);
  }
  return payload.status;
}

/** Pure: map action failures. */
export function toCaseActionError(err: unknown): CaseActionError {
  if (err instanceof CaseActionError) {
    return err;
  }
  if (err instanceof HttpErrorResponse) {
    return new CaseActionError(CASES_REVIEW_MESSAGES.actionNetworkFailed, 'NETWORK');
  }
  return new CaseActionError(CASES_REVIEW_MESSAGES.actionFailed);
}

/** Pure: map download failures. */
export function toCaseDownloadError(err: unknown): CaseDownloadError {
  if (err instanceof CaseDownloadError) {
    return err;
  }
  if (err instanceof HttpErrorResponse) {
    if (err.status === 404) {
      return new CaseDownloadError(CASES_REVIEW_MESSAGES.downloadNotFound, 'NOT_FOUND');
    }
    if (err.status === 0) {
      return new CaseDownloadError(CASES_REVIEW_MESSAGES.downloadNetworkFailed, 'NETWORK');
    }
    return new CaseDownloadError(CASES_REVIEW_MESSAGES.downloadFailed, 'NETWORK');
  }
  return new CaseDownloadError(CASES_REVIEW_MESSAGES.downloadFailed);
}

function formatFormFieldValue(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }
  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }
  return JSON.stringify(value);
}

function humanizeFormKey(key: string): string {
  if ((CASE_FORM_FIELD_KEYS as readonly string[]).includes(key)) {
    return CASE_FORM_FIELD_LABELS[key as CaseFormFieldKey];
  }
  const spaced: string = key
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .trim();
  if (!spaced) {
    return key;
  }
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/** Pure: loose UUID check for route params (API uses UUID!). */
export function isCaseId(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}
