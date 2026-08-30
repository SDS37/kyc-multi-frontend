import { FormControl } from '@angular/forms';
import { GraphqlError } from '../shared/graphql.models';

/** GraphQL `CaseStatus` enum values (Hot Chocolate names). */
export const CASE_STATUSES = [
  'DRAFT',
  'SUBMITTED',
  'IN_REVIEW',
  'APPROVED',
  'REJECTED',
] as const;

export type CaseStatus = (typeof CASE_STATUSES)[number];

export const CASE_STATUS_LABELS: Readonly<Record<CaseStatus, string>> = {
  DRAFT: 'Draft',
  SUBMITTED: 'Submitted',
  IN_REVIEW: 'In review',
  APPROVED: 'Approved',
  REJECTED: 'Rejected',
};

export function isCaseStatus(value: string): value is CaseStatus {
  return (CASE_STATUSES as readonly string[]).includes(value);
}

export function caseStatusLabel(status: CaseStatus): string {
  return CASE_STATUS_LABELS[status];
}

/** Max length for approve/reject review comments (API validation). */
export const REVIEW_COMMENT_MAX_LENGTH: number = 2000;

/** Known FormData keys from submit validation (KYC-033). */
export const CASE_FORM_FIELD_KEYS = [
  'fullName',
  'dateOfBirth',
  'nationality',
  'address',
] as const;

export type CaseFormFieldKey = (typeof CASE_FORM_FIELD_KEYS)[number];

export const CASE_FORM_FIELD_LABELS: Readonly<Record<CaseFormFieldKey, string>> = {
  fullName: 'Full name',
  dateOfBirth: 'Date of birth',
  nationality: 'Nationality',
  address: 'Address',
};

/** One row from GraphQL `cases.items` (KYC-036 / KYC-062). */
export interface CaseListItem {
  id: string;
  title: string;
  status: CaseStatus;
  customerEmail: string;
  updatedAt: string;
}

/** Paginated `cases` query result. */
export interface CaseListPage {
  items: CaseListItem[];
  totalCount: number;
  skip: number;
  take: number;
}

/** Inputs for `CasesService.list`. */
export interface ListCasesParams {
  status?: CaseStatus | null;
  skip?: number;
  take?: number;
}

/** Display row for parsed FormData (known keys + any extras). */
export interface CaseFormField {
  key: string;
  label: string;
  value: string;
}

/** Document metadata from `case.documents` (never bytes). */
export interface CaseDocument {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  uploadedBy: string;
}

/** Review comment entry from `case.comments`. */
export interface CaseComment {
  text: string;
  createdAt: string;
  authorUserId: string;
}

/** Full case detail for the review page (KYC-063). */
export interface CaseDetail {
  id: string;
  title: string;
  status: CaseStatus;
  customerEmail: string;
  customerUserId: string;
  formFields: readonly CaseFormField[];
  formDataRaw: string;
  reviewComment: string | null;
  reviewedAt: string | null;
  reviewedBy: string | null;
  submittedAt: string | null;
  createdAt: string;
  updatedAt: string;
  comments: readonly CaseComment[];
  documents: readonly CaseDocument[];
}

/** Which review actions the UI may offer for a status. */
export interface CaseReviewActions {
  canStartReview: boolean;
  canApprove: boolean;
  canReject: boolean;
}

/** Reject form control map (Reactive Forms; single required comment). */
export interface RejectFormControls {
  comment: FormControl<string>;
}

/** User-facing case list / detail load failure. */
export class CasesLoadError extends Error {
  constructor(
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'CasesLoadError';
  }
}

/** User-facing review action failure (start / approve / reject). */
export class CaseActionError extends Error {
  constructor(
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'CaseActionError';
  }
}

/** User-facing document download failure. */
export class CaseDownloadError extends Error {
  constructor(
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'CaseDownloadError';
  }
}

/** Wire shape for GraphQL `cases` HTTP body (cases feature only). */
export interface GraphqlCasesBody {
  data?: {
    cases?: {
      totalCount?: number;
      skip?: number;
      take?: number;
      items?: Array<{
        id?: string;
        title?: string;
        status?: string;
        customerEmail?: string;
        updatedAt?: string;
      } | null> | null;
    } | null;
  };
  errors?: GraphqlError[];
}

/** Wire shape for GraphQL `case(id)` detail body. */
export interface GraphqlCaseDetailBody {
  data?: {
    case?: {
      case?: {
        id?: string;
        title?: string;
        status?: string;
        formData?: string;
        customerUserId?: string;
        customerEmail?: string;
        createdAt?: string;
        updatedAt?: string;
        submittedAt?: string | null;
        reviewedAt?: string | null;
        reviewedBy?: string | null;
        reviewComment?: string | null;
      } | null;
      comments?: Array<{
        text?: string;
        createdAt?: string;
        authorUserId?: string;
      } | null> | null;
      documents?: Array<{
        id?: string;
        fileName?: string;
        contentType?: string;
        sizeBytes?: number;
        uploadedAt?: string;
        uploadedBy?: string;
      } | null> | null;
    } | null;
  };
  errors?: GraphqlError[];
}

/** Wire shape for start / approve / reject mutations returning a case summary. */
export interface GraphqlCaseActionBody {
  data?: {
    startCaseReview?: { id?: string; status?: string; updatedAt?: string } | null;
    approveCase?: {
      id?: string;
      status?: string;
      reviewedAt?: string | null;
      reviewedBy?: string | null;
      reviewComment?: string | null;
    } | null;
    rejectCase?: {
      id?: string;
      status?: string;
      reviewedAt?: string | null;
      reviewedBy?: string | null;
      reviewComment?: string | null;
    } | null;
  };
  errors?: GraphqlError[];
}
