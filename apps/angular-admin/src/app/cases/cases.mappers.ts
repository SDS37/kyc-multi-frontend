import { HttpErrorResponse } from '@angular/common/http';
import { GraphqlError } from '../shared/graphql.models';
import {
  CASE_FORM_FIELD_KEYS,
  CASE_FORM_FIELD_LABELS,
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
      gqlError.message?.trim() || 'Unable to load cases. Try again.',
      gqlError.extensions?.code,
    );
  }

  const page = body.data?.cases;
  if (!page || !Array.isArray(page.items)) {
    throw new CasesLoadError('Unable to load cases. Try again.');
  }

  const items: CaseListItem[] = page.items.map(
    (raw): CaseListItem => {
      if (!raw?.id || !raw.title || !raw.customerEmail || !raw.updatedAt || !raw.status) {
        throw new CasesLoadError('Case list response was incomplete.');
      }
      if (!isCaseStatus(raw.status)) {
        throw new CasesLoadError(`Unexpected case status: ${raw.status}`);
      }
      return {
        id: raw.id,
        title: raw.title,
        status: raw.status,
        customerEmail: raw.customerEmail,
        updatedAt: raw.updatedAt,
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
    return new CasesLoadError(
      'Unable to reach the cases service. Try again in a moment.',
      'NETWORK',
    );
  }
  return new CasesLoadError('Unable to load cases. Try again.');
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
    return [{ key: 'formData', label: 'Form data', value: trimmed }];
  }

  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return [{ key: 'formData', label: 'Form data', value: trimmed }];
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
      gqlError.message?.trim() || 'Unable to load this case. Try again.',
      gqlError.extensions?.code,
    );
  }

  const envelope = body.data?.case;
  const raw = envelope?.case;
  if (!raw?.id || !raw.title || !raw.status || !raw.customerEmail || !raw.customerUserId) {
    throw new CasesLoadError('Case detail response was incomplete.');
  }
  if (!raw.createdAt || !raw.updatedAt || raw.formData === undefined || raw.formData === null) {
    throw new CasesLoadError('Case detail response was incomplete.');
  }
  if (!isCaseStatus(raw.status)) {
    throw new CasesLoadError(`Unexpected case status: ${raw.status}`);
  }

  const comments: CaseComment[] = (envelope?.comments ?? [])
    .filter((c): c is NonNullable<typeof c> => c != null)
    .map((c): CaseComment => {
      if (!c.text || !c.createdAt || !c.authorUserId) {
        throw new CasesLoadError('Case comments response was incomplete.');
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
        throw new CasesLoadError('Case documents response was incomplete.');
      }
      return {
        id: d.id,
        fileName: d.fileName,
        contentType: d.contentType,
        sizeBytes: d.sizeBytes,
        uploadedAt: d.uploadedAt,
        uploadedBy: d.uploadedBy,
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
    return { ok: false, message: 'A comment is required to reject a case.' };
  }
  if (comment.length > REVIEW_COMMENT_MAX_LENGTH) {
    return {
      ok: false,
      message: `Comment must be at most ${REVIEW_COMMENT_MAX_LENGTH} characters.`,
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
      message: `Comment must be at most ${REVIEW_COMMENT_MAX_LENGTH} characters.`,
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
      gqlError.message?.trim() || 'Unable to complete that action. Try again.',
      gqlError.extensions?.code,
    );
  }

  const payload = body.data?.[kind];
  if (!payload?.status || !isCaseStatus(payload.status)) {
    throw new CaseActionError('Action response was incomplete.');
  }
  return payload.status;
}

/** Pure: map action failures. */
export function toCaseActionError(err: unknown): CaseActionError {
  if (err instanceof CaseActionError) {
    return err;
  }
  if (err instanceof HttpErrorResponse) {
    return new CaseActionError(
      'Unable to reach the cases service. Try again in a moment.',
      'NETWORK',
    );
  }
  return new CaseActionError('Unable to complete that action. Try again.');
}

/** Pure: map download failures. */
export function toCaseDownloadError(err: unknown): CaseDownloadError {
  if (err instanceof CaseDownloadError) {
    return err;
  }
  if (err instanceof HttpErrorResponse) {
    if (err.status === 404) {
      return new CaseDownloadError('Document was not found.', 'NOT_FOUND');
    }
    if (err.status === 0) {
      return new CaseDownloadError(
        'Unable to reach the download service. Try again in a moment.',
        'NETWORK',
      );
    }
    return new CaseDownloadError(
      'Unable to download this document. Try again.',
      'NETWORK',
    );
  }
  return new CaseDownloadError('Unable to download this document. Try again.');
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
