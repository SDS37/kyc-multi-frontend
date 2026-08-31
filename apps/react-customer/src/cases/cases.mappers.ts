import type { GraphqlError, GraphqlResponse } from '../shared/graphql.models';
import {
  CASE_STATUS_LABELS,
  CASES_LIST_MESSAGES,
  caseStatusLabel,
  openCaseAriaLabel,
  unexpectedCaseStatusMessage,
} from './cases.messages';
import {
  CREATE_DRAFT_TITLE_MAX_LENGTH,
  type CaseListItem,
  type CaseListPage,
  type CaseStatus,
  CasesLoadError,
  type CreateDraftCaseInput,
  CreateDraftError,
  type CreatedDraftCase,
  type GraphqlCasesBody,
  type GraphqlCreateDraftBody,
  type ListCasesParams,
  type ListCasesVariables,
  isCaseStatus,
} from './cases.models';

export { caseStatusLabel, CASE_STATUS_LABELS };

const UPDATED_AT_FORMATTER: Intl.DateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
});

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

function formatUpdatedAt(iso: string): string {
  const date: Date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return UPDATED_AT_FORMATTER.format(date);
}
