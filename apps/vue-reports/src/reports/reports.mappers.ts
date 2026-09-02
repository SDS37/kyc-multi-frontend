import { GraphqlHttpError, type GraphqlError, type GraphqlResponse } from '../shared/graphql.models';
import { RATE_LIMITED_CODE, RATE_LIMITED_HTTP_STATUS } from '../auth/auth.models';
import {
  CASE_STATUS_LABELS,
  REPORTS_HOME_MESSAGES,
  caseStatusLabel,
  unexpectedCaseStatusMessage,
} from './reports.messages';
import {
  LATEST_CASES_TAKE,
  type CaseStatus,
  type GraphqlCasesCountPage,
  type GraphqlReportsOverviewBody,
  type GraphqlReportsOverviewData,
  type ReportCaseRow,
  ReportsLoadError,
  type ReportsOverview,
  type StatusCount,
  isCaseStatus,
} from './reports.models';

export { CASE_STATUS_LABELS, caseStatusLabel };

const UPDATED_AT_FORMATTER: Intl.DateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
});

const COUNT_ALIASES: ReadonlyArray<{
  readonly status: CaseStatus;
  readonly key: keyof Pick<
    GraphqlReportsOverviewData,
    'draft' | 'submitted' | 'inReview' | 'approved' | 'rejected'
  >;
}> = [
  { status: 'DRAFT', key: 'draft' },
  { status: 'SUBMITTED', key: 'submitted' },
  { status: 'IN_REVIEW', key: 'inReview' },
  { status: 'APPROVED', key: 'approved' },
  { status: 'REJECTED', key: 'rejected' },
];

/** Pure: map aliased GraphQL overview body → DTO. */
export function parseReportsOverview(
  body: GraphqlReportsOverviewBody | GraphqlResponse<GraphqlReportsOverviewData>,
): ReportsOverview {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw new ReportsLoadError(
      gqlError.message?.trim() || REPORTS_HOME_MESSAGES.listLoadFailed,
      gqlError.extensions?.code,
    );
  }

  const data: GraphqlReportsOverviewData | null | undefined = body.data;
  if (!data) {
    throw new ReportsLoadError(REPORTS_HOME_MESSAGES.listLoadFailed);
  }

  const counts: StatusCount[] = COUNT_ALIASES.map(
    ({ status, key }): StatusCount => ({
      status,
      label: caseStatusLabel(status),
      count: parseTotalCount(data[key]),
    }),
  );

  const latestPage = data.latest;
  if (!latestPage || !Array.isArray(latestPage.items)) {
    throw new ReportsLoadError(REPORTS_HOME_MESSAGES.listIncomplete);
  }

  const latest: ReportCaseRow[] = latestPage.items
    .slice(0, LATEST_CASES_TAKE)
    .map((raw): ReportCaseRow => {
      if (
        !raw?.id ||
        !raw.title ||
        !raw.updatedAt ||
        !raw.status ||
        typeof raw.customerEmail !== 'string'
      ) {
        throw new ReportsLoadError(REPORTS_HOME_MESSAGES.listIncomplete);
      }
      if (!isCaseStatus(raw.status)) {
        throw new ReportsLoadError(unexpectedCaseStatusMessage(raw.status));
      }
      return {
        id: raw.id,
        title: raw.title,
        status: raw.status,
        statusLabel: caseStatusLabel(raw.status),
        customerEmail: raw.customerEmail,
        updatedAt: raw.updatedAt,
        updatedAtLabel: formatUpdatedAt(raw.updatedAt),
      };
    });

  return {
    counts,
    latest,
    latestTotalCount: parseOptionalTotalCount(latestPage.totalCount, latest.length),
  };
}

/** Pure: map transport / unknown errors to ReportsLoadError. */
export function toReportsLoadError(err: unknown): ReportsLoadError {
  if (err instanceof ReportsLoadError) {
    return err;
  }
  if (err instanceof GraphqlHttpError) {
    if (err.status === RATE_LIMITED_HTTP_STATUS) {
      return new ReportsLoadError(REPORTS_HOME_MESSAGES.listRateLimited, RATE_LIMITED_CODE);
    }
    return new ReportsLoadError(REPORTS_HOME_MESSAGES.listLoadFailed, 'NETWORK');
  }
  if (err instanceof TypeError) {
    return new ReportsLoadError(REPORTS_HOME_MESSAGES.listLoadFailed, 'NETWORK');
  }
  if (err instanceof Error && /GraphQL HTTP|Failed to fetch|NetworkError/i.test(err.message)) {
    return new ReportsLoadError(REPORTS_HOME_MESSAGES.listLoadFailed, 'NETWORK');
  }
  return new ReportsLoadError(REPORTS_HOME_MESSAGES.listLoadFailed);
}

export function formatUpdatedAt(iso: string): string {
  const date: Date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return UPDATED_AT_FORMATTER.format(date);
}

function parseTotalCount(page: GraphqlCasesCountPage | null | undefined): number {
  if (
    !page ||
    typeof page.totalCount !== 'number' ||
    !Number.isInteger(page.totalCount) ||
    page.totalCount < 0
  ) {
    throw new ReportsLoadError(REPORTS_HOME_MESSAGES.listIncomplete);
  }
  return page.totalCount;
}

function parseOptionalTotalCount(raw: number | undefined, fallback: number): number {
  if (typeof raw === 'number' && Number.isInteger(raw) && raw >= 0) {
    return raw;
  }
  return fallback;
}
