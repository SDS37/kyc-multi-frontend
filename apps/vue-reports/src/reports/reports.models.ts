import type { GraphqlError } from '../shared/graphql.models';

/** GraphQL `CaseStatus` enum values (Hot Chocolate names). */
export type CaseStatus = 'DRAFT' | 'SUBMITTED' | 'IN_REVIEW' | 'APPROVED' | 'REJECTED';

export const CASE_STATUSES: readonly CaseStatus[] = [
  'DRAFT',
  'SUBMITTED',
  'IN_REVIEW',
  'APPROVED',
  'REJECTED',
];

export function isCaseStatus(value: string): value is CaseStatus {
  return (CASE_STATUSES as readonly string[]).includes(value);
}

/** Latest-N page size for the overview table (KYC-081). */
export const LATEST_CASES_TAKE: number = 10;

/** Count-only alias uses take: 1 because the API rejects take < 1. */
export const COUNT_ALIAS_TAKE: number = 1;

/** One status bucket for the counts strip. */
export interface StatusCount {
  readonly status: CaseStatus;
  readonly label: string;
  readonly count: number;
}

/** One row in the latest-cases table (display only). */
export interface ReportCaseRow {
  readonly id: string;
  readonly title: string;
  readonly status: CaseStatus;
  readonly statusLabel: string;
  readonly customerEmail: string;
  readonly updatedAt: string;
  readonly updatedAtLabel: string;
}

/** Parsed `/reports` overview. */
export interface ReportsOverview {
  readonly counts: readonly StatusCount[];
  readonly latest: readonly ReportCaseRow[];
  readonly latestTotalCount: number;
}

/** Wire `cases` page used by count aliases. */
export interface GraphqlCasesCountPage {
  readonly totalCount?: number;
}

/** Wire `cases` page used by the latest alias. */
export interface GraphqlCasesLatestPage {
  readonly totalCount?: number;
  readonly items?: ReadonlyArray<{
    readonly id?: string;
    readonly title?: string;
    readonly status?: string;
    readonly customerEmail?: string;
    readonly updatedAt?: string;
  } | null> | null;
}

/** Aliased overview query body (KYC-081). */
export interface GraphqlReportsOverviewData {
  readonly draft?: GraphqlCasesCountPage | null;
  readonly submitted?: GraphqlCasesCountPage | null;
  readonly inReview?: GraphqlCasesCountPage | null;
  readonly approved?: GraphqlCasesCountPage | null;
  readonly rejected?: GraphqlCasesCountPage | null;
  readonly latest?: GraphqlCasesLatestPage | null;
}

export interface GraphqlReportsOverviewBody {
  readonly data?: GraphqlReportsOverviewData | null;
  readonly errors?: GraphqlError[];
}

/** User-facing overview load failure. */
export class ReportsLoadError extends Error {
  readonly code?: string;

  constructor(message: string, code?: string) {
    super(message);
    this.name = 'ReportsLoadError';
    this.code = code;
  }
}
