import type { CaseStatus } from './reports.models';

/** Case status labels (same copy as Angular / React). */
export const CASE_STATUS_LABELS: Readonly<Record<CaseStatus, string>> = {
  DRAFT: 'Draft',
  SUBMITTED: 'Submitted',
  IN_REVIEW: 'In review',
  APPROVED: 'Approved',
  REJECTED: 'Rejected',
};

export function caseStatusLabel(status: CaseStatus): string {
  return CASE_STATUS_LABELS[status];
}

export function unexpectedCaseStatusMessage(raw: string): string {
  return `Unexpected case status "${raw}".`;
}

/** Reports home chrome (KYC-081). */
export interface ReportsHomeMessages {
  readonly pageTitle: string;
  readonly lede: string;
  readonly countsHeading: string;
  readonly latestHeading: string;
  readonly latestHint: string;
  readonly loading: string;
  readonly loadingAria: string;
  readonly emptyLatest: string;
  readonly columnTitle: string;
  readonly columnCustomer: string;
  readonly columnStatus: string;
  readonly columnUpdated: string;
  readonly listLoadFailed: string;
  readonly listIncomplete: string;
  readonly listRateLimited: string;
}

export const REPORTS_HOME_MESSAGES: ReportsHomeMessages = {
  pageTitle: 'Reports',
  lede: 'Tenant-wide case overview for reviewers and tenant admins.',
  countsHeading: 'Cases by status',
  latestHeading: 'Latest cases',
  latestHint: 'Newest 10 cases in this tenant (same list order as the API).',
  loading: 'Loading reports…',
  loadingAria: 'Loading reports',
  emptyLatest: 'No cases yet for this tenant.',
  columnTitle: 'Title',
  columnCustomer: 'Customer',
  columnStatus: 'Status',
  columnUpdated: 'Updated',
  listLoadFailed: 'Unable to load reports. Try again.',
  listIncomplete: 'Reports response was incomplete.',
  listRateLimited: 'Too many requests. Wait a minute and try again.',
};

const CASE_SINGULAR: string = 'case';
const CASE_PLURAL: string = 'cases';

/** Pure: latest-table count line; honest when the page is truncated. */
export function reportsLatestCountLabel(shownCount: number, totalCount: number): string {
  const unit: string = totalCount === 1 ? CASE_SINGULAR : CASE_PLURAL;
  if (shownCount < totalCount) {
    return `Showing ${String(shownCount)} of ${String(totalCount)} ${unit}`;
  }
  return `${String(totalCount)} ${unit}`;
}
