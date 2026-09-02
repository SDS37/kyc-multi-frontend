import type { CaseFormFieldKey, CaseStatus } from './cases.models';
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

/** Known FormData field labels (KYC-033 / KYC-073 — match Angular admin). */
export const CASE_FORM_FIELD_LABELS: Readonly<Record<CaseFormFieldKey, string>> = {
  fullName: 'Full name',
  dateOfBirth: 'Date of birth',
  nationality: 'Nationality',
  address: 'Address',
};

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
  readonly listRateLimited: string;
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
  readonly createRateLimited: string;
  readonly createUnauthorized: string;
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
  listRateLimited: 'Too many requests. Wait a minute and try again.',
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
  createRateLimited: 'Too many requests. Wait a minute and try again.',
  createUnauthorized:
    'Your account cannot create cases. Sign in with a Customer user.',
  backToCases: '← Back to my cases',
};

/** Draft editor chrome (KYC-073). */
export interface CasesDraftMessages {
  readonly pageTitleFallback: string;
  readonly ledeEdit: string;
  readonly ledeReadonly: string;
  readonly loading: string;
  readonly loadingAria: string;
  readonly invalidCaseLink: string;
  readonly loadFailed: string;
  readonly loadIncomplete: string;
  readonly loadNetworkFailed: string;
  readonly loadRateLimited: string;
  readonly loadNotFound: string;
  readonly sectionPerson: string;
  readonly sectionCompany: string;
  readonly companyNameLabel: string;
  readonly companyOptionalHint: string;
  readonly dateOfBirthHint: string;
  readonly dateOfBirthInvalid: string;
  readonly saveDraft: string;
  readonly savingDraft: string;
  readonly submit: string;
  readonly submitting: string;
  readonly saveSuccess: string;
  readonly submitSuccess: string;
  readonly saveFailed: string;
  readonly saveIncomplete: string;
  readonly saveNetworkFailed: string;
  readonly saveRateLimited: string;
  readonly submitFailed: string;
  readonly submitIncomplete: string;
  readonly submitNetworkFailed: string;
  readonly submitRateLimited: string;
  readonly actionDomain: string;
  readonly actionNotFound: string;
  readonly actionUnauthorized: string;
  readonly updatedLabel: string;
  readonly submittedLabel: string;
  readonly readonlyNotice: string;
  readonly docsHeading: string;
  readonly docsEmpty: string;
  readonly docsUploadLabel: string;
  readonly docsUploading: string;
  readonly docsAcceptHint: string;
  readonly docsTypeRejected: string;
  readonly docsSizeRejected: string;
  readonly docsEmptyFile: string;
  readonly docsUploadFailed: string;
  readonly docsUploadIncomplete: string;
  readonly docsUploadNetworkFailed: string;
  readonly docsUploadRateLimited: string;
  readonly docsUploadDomain: string;
  readonly docsUploadUnauthorized: string;
  readonly docsUploadNotFound: string;
  readonly docsUploadStorage: string;
  readonly docsIncomplete: string;
}

export const CASES_DRAFT_MESSAGES: CasesDraftMessages = {
  pageTitleFallback: 'Case',
  ledeEdit: 'Fill in your details, save the draft anytime, then submit when ready.',
  ledeReadonly: 'This case is no longer a draft. You can review the details below.',
  loading: 'Loading case…',
  loadingAria: 'Loading case',
  invalidCaseLink: 'This case link is not valid.',
  loadFailed: 'Unable to load this case. Try again.',
  loadIncomplete: 'Case detail response was incomplete.',
  loadNetworkFailed: 'Unable to reach the cases service. Try again in a moment.',
  loadRateLimited: 'Too many requests. Wait a minute and try again.',
  loadNotFound: 'Case was not found.',
  sectionPerson: 'Personal details',
  sectionCompany: 'Company (optional)',
  companyNameLabel: 'Company name',
  companyOptionalHint: 'Optional — leave blank if not applicable.',
  dateOfBirthHint: 'Use YYYY-MM-DD.',
  dateOfBirthInvalid: 'Date of birth must be an ISO date (YYYY-MM-DD).',
  saveDraft: 'Save draft',
  savingDraft: 'Saving…',
  submit: 'Submit case',
  submitting: 'Submitting…',
  saveSuccess: 'Draft saved.',
  submitSuccess: 'Case submitted.',
  saveFailed: 'Unable to save the draft. Try again.',
  saveIncomplete: 'Save draft response was incomplete.',
  saveNetworkFailed: 'Unable to reach the cases service. Try again in a moment.',
  saveRateLimited: 'Too many requests. Wait a minute and try again.',
  submitFailed: 'Unable to submit the case. Try again.',
  submitIncomplete: 'Submit response was incomplete.',
  submitNetworkFailed: 'Unable to reach the cases service. Try again in a moment.',
  submitRateLimited: 'Too many requests. Wait a minute and try again.',
  actionDomain: 'Only draft cases can be updated or submitted.',
  actionNotFound: 'Case was not found.',
  actionUnauthorized: 'Your account cannot update this case.',
  updatedLabel: 'Updated',
  submittedLabel: 'Submitted',
  readonlyNotice: 'Editing is only available while the case is a draft.',
  docsHeading: 'Documents',
  docsEmpty: 'No documents uploaded.',
  docsUploadLabel: 'Upload file',
  docsUploading: 'Uploading…',
  docsAcceptHint: 'PDF, PNG, or JPG up to 10 MB.',
  docsTypeRejected: 'File type must be PDF, PNG, or JPG.',
  docsSizeRejected: 'File must be at most 10 MB.',
  docsEmptyFile: 'A non-empty file is required.',
  docsUploadFailed: 'Unable to upload the document. Try again.',
  docsUploadIncomplete: 'Upload response was incomplete.',
  docsUploadNetworkFailed: 'Unable to reach the documents service. Try again in a moment.',
  docsUploadRateLimited: 'Too many requests. Wait a minute and try again.',
  docsUploadDomain: 'Documents can only be uploaded to draft or submitted cases.',
  docsUploadUnauthorized: 'Your account cannot upload documents.',
  docsUploadNotFound: 'Case was not found.',
  docsUploadStorage: 'Could not store the document. Please try again.',
  docsIncomplete: 'Document list response was incomplete.',
};

/** Pure: required-field message for a FormData key (API-shaped). */
export function draftFieldRequiredMessage(field: CaseFormFieldKey): string {
  return `${field} is required.`;
}

/** Pure: list count line; honest when the page is truncated. */
export function casesCountLabel(shownCount: number, totalCount: number): string {
  const unit: string =
    totalCount === 1 ? CASES_LIST_MESSAGES.caseSingular : CASES_LIST_MESSAGES.casePlural;
  if (shownCount < totalCount) {
    return `Showing ${String(shownCount)} of ${String(totalCount)} ${unit}`;
  }
  return `${String(totalCount)} ${unit}`;
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
