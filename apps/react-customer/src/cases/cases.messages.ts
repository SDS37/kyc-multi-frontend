import type { CaseStatus } from './cases.models';
import { CREATE_DRAFT_TITLE_MAX_LENGTH } from './cases.models';

/** Case status labels for filters / badges. */
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

/** Customer my-cases chrome (KYC-072). */
export interface CasesListMessages {
  readonly pageTitle: string;
  readonly lede: string;
  readonly statusFilterLabel: string;
  readonly statusFilterAria: string;
  readonly allStatuses: string;
  readonly loading: string;
  readonly loadingAria: string;
  readonly emptyAll: string;
  readonly columnTitle: string;
  readonly columnStatus: string;
  readonly columnUpdated: string;
  readonly caseSingular: string;
  readonly casePlural: string;
  readonly openCaseAriaPrefix: string;
  readonly listLoadFailed: string;
  readonly listIncomplete: string;
  readonly listNetworkFailed: string;
  readonly createAction: string;
  readonly createDialogTitle: string;
  readonly createTitleLabel: string;
  readonly createTitleRequired: string;
  readonly createTitleMaxLength: string;
  readonly createSubmit: string;
  readonly createSubmitting: string;
  readonly createCancel: string;
  readonly createFailed: string;
  readonly createIncomplete: string;
  readonly createNetworkFailed: string;
  readonly createUnauthorized: string;
  readonly draftPlaceholderTitle: string;
  readonly draftPlaceholderLede: string;
  readonly backToCases: string;
}

export const CASES_LIST_MESSAGES: CasesListMessages = {
  pageTitle: 'My cases',
  lede: 'Cases you own in this tenant. Create a draft to get started.',
  statusFilterLabel: 'Status',
  statusFilterAria: 'Filter cases by status',
  allStatuses: 'All statuses',
  loading: 'Loading cases…',
  loadingAria: 'Loading cases',
  emptyAll: 'No cases yet. Create a draft to begin.',
  columnTitle: 'Title',
  columnStatus: 'Status',
  columnUpdated: 'Updated',
  caseSingular: 'case',
  casePlural: 'cases',
  openCaseAriaPrefix: 'Open case',
  listLoadFailed: 'Unable to load cases. Try again.',
  listIncomplete: 'Case list response was incomplete.',
  listNetworkFailed: 'Unable to reach the cases service. Try again in a moment.',
  createAction: 'New case',
  createDialogTitle: 'Create draft case',
  createTitleLabel: 'Title',
  createTitleRequired: 'Title is required.',
  createTitleMaxLength: `Title must be at most ${CREATE_DRAFT_TITLE_MAX_LENGTH} characters.`,
  createSubmit: 'Create draft',
  createSubmitting: 'Creating…',
  createCancel: 'Cancel',
  createFailed: 'Unable to create the draft. Try again.',
  createIncomplete: 'Create draft response was incomplete.',
  createNetworkFailed: 'Unable to reach the cases service. Try again in a moment.',
  createUnauthorized:
    'Your account cannot create cases. Sign in with a Customer user.',
  draftPlaceholderTitle: 'Draft case',
  draftPlaceholderLede:
    'Form fields and submit arrive in the next story. Your draft is saved.',
  backToCases: '← Back to my cases',
};

/** Pure: list count line (“1 case” / “3 cases”). */
export function casesCountLabel(totalCount: number): string {
  const unit: string =
    totalCount === 1 ? CASES_LIST_MESSAGES.caseSingular : CASES_LIST_MESSAGES.casePlural;
  return `${totalCount} ${unit}`;
}

/** Pure: empty list when a status filter is active. */
export function casesEmptyForStatusLabel(status: CaseStatus): string {
  return `No cases with status ${caseStatusLabel(status)}.`;
}

/** Pure: row aria-label for opening a case. */
export function openCaseAriaLabel(title: string): string {
  return `${CASES_LIST_MESSAGES.openCaseAriaPrefix} ${title}`;
}

/** Pure: unexpected status from the API. */
export function unexpectedCaseStatusMessage(status: string): string {
  return `Unexpected case status: ${status}`;
}
