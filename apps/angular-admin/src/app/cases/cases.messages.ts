import {
  CaseFormFieldKey,
  CaseStatus,
  REVIEW_COMMENT_MAX_LENGTH,
} from './cases.models';

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

/** Known FormData field labels (KYC-033 / KYC-063). */
export const CASE_FORM_FIELD_LABELS: Readonly<Record<CaseFormFieldKey, string>> = {
  fullName: 'Full name',
  dateOfBirth: 'Date of birth',
  nationality: 'Nationality',
  address: 'Address',
};

/** Case list chrome (KYC-062). */
export const CASES_LIST_MESSAGES = {
  pageTitle: 'Cases',
  lede: 'Review queue for your tenant. Filter by status to pick work.',
  statusFilterLabel: 'Status',
  statusFilterAria: 'Filter cases by status',
  allStatuses: 'All statuses',
  loading: 'Loading cases…',
  loadingAria: 'Loading cases',
  emptyAll: 'No cases yet for this tenant.',
  columnTitle: 'Title',
  columnCustomer: 'Customer',
  columnStatus: 'Status',
  columnUpdated: 'Updated',
  caseSingular: 'case',
  casePlural: 'cases',
  openCaseAriaPrefix: 'Open case',
  listLoadFailed: 'Unable to load cases. Try again.',
  listIncomplete: 'Case list response was incomplete.',
  listNetworkFailed: 'Unable to reach the cases service. Try again in a moment.',
} as const;

/** Case review chrome (KYC-063). */
export const CASES_REVIEW_MESSAGES = {
  backToCases: '← Back to cases',
  backToCasesShort: 'Back to cases',
  fallbackTitle: 'Case review',
  loading: 'Loading case…',
  loadingAria: 'Loading case',
  formHeading: 'Application form',
  formEmpty: 'No form fields on this case.',
  formDataFallbackLabel: 'Form data',
  docsHeading: 'Documents',
  docsEmpty: 'No documents uploaded.',
  download: 'Download',
  downloading: 'Downloading…',
  downloadAriaPrefix: 'Download',
  commentsHeading: 'Review notes',
  actionsHeading: 'Actions',
  startReviewLede: 'This case is submitted and ready for review.',
  startReview: 'Start review',
  approveCommentLabel: 'Optional approve comment',
  approveCommentHint: 'Optional when approving',
  approve: 'Approve',
  rejectCommentLabel: 'Reject comment (required)',
  rejectCommentRequired: 'A comment is required to reject.',
  reject: 'Reject',
  invalidCaseLink: 'This case link is not valid.',
  loadFailed: 'Unable to load this case. Try again.',
  loadIncomplete: 'Case detail response was incomplete.',
  commentsIncomplete: 'Case comments response was incomplete.',
  documentsIncomplete: 'Case documents response was incomplete.',
  rejectCommentRequiredAction: 'A comment is required to reject a case.',
  actionFailed: 'Unable to complete that action. Try again.',
  actionIncomplete: 'Action response was incomplete.',
  actionNetworkFailed: 'Unable to reach the cases service. Try again in a moment.',
  downloadNotFound: 'Document was not found.',
  downloadNetworkFailed: 'Unable to reach the download service. Try again in a moment.',
  downloadFailed: 'Unable to download this document. Try again.',
} as const;

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

/** Pure: document download button aria-label. */
export function downloadDocumentAriaLabel(fileName: string): string {
  return `${CASES_REVIEW_MESSAGES.downloadAriaPrefix} ${fileName}`;
}

/** Pure: reject maxlength hint / error. */
export function rejectCommentMaxLengthMessage(): string {
  return `Comment must be at most ${REVIEW_COMMENT_MAX_LENGTH} characters.`;
}

/** Pure: reject hint under the field. */
export function rejectCommentHint(): string {
  return `Required to reject · max ${REVIEW_COMMENT_MAX_LENGTH}`;
}

/** Pure: no actions available for the current status. */
export function noReviewActionsMessage(statusLabel: string): string {
  return `No review actions available for status ${statusLabel}.`;
}

/** Pure: unexpected status from the API. */
export function unexpectedCaseStatusMessage(status: string): string {
  return `Unexpected case status: ${status}`;
}
