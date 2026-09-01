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

/** Max title length for create/update draft (API validation). */
export const CREATE_DRAFT_TITLE_MAX_LENGTH: number = 200;

/** Known person FormData keys (KYC-033 / KYC-073). */
export const CASE_FORM_FIELD_KEYS = [
  'fullName',
  'dateOfBirth',
  'nationality',
  'address',
] as const;

export type CaseFormFieldKey = (typeof CASE_FORM_FIELD_KEYS)[number];

/** Optional company FormData key (API does not require it). */
export const OPTIONAL_COMPANY_FIELD_KEY = 'companyName' as const;

/** One row from GraphQL `cases.items` (customer list — no customerEmail). */
export interface CaseListItem {
  readonly id: string;
  readonly title: string;
  readonly status: CaseStatus;
  readonly statusLabel: string;
  readonly updatedAt: string;
  readonly updatedAtLabel: string;
  readonly openAriaLabel: string;
}

/** Paginated `cases` query result. */
export interface CaseListPage {
  readonly items: readonly CaseListItem[];
  readonly totalCount: number;
  readonly skip: number;
  readonly take: number;
}

/** Inputs for listCases. */
export interface ListCasesParams {
  readonly status?: CaseStatus | null;
  readonly skip?: number;
  readonly take?: number;
}

export interface ListCasesVariables {
  readonly status: CaseStatus | null;
  readonly skip: number;
  readonly take: number;
}

/** Created draft summary (KYC-072 create CTA). */
export interface CreatedDraftCase {
  readonly id: string;
  readonly title: string;
  readonly status: CaseStatus;
  readonly updatedAt: string;
}

export interface CreateDraftCaseInput {
  readonly title: string;
}

/** Editable draft form state (KYC-073). */
export interface DraftFormModel {
  readonly title: string;
  readonly fullName: string;
  readonly dateOfBirth: string;
  readonly nationality: string;
  readonly address: string;
  readonly companyName: string;
}

export type DraftFormFieldErrors = {
  readonly title?: string;
  readonly fullName?: string;
  readonly dateOfBirth?: string;
  readonly nationality?: string;
  readonly address?: string;
};

/** Case detail for the draft editor (subset of GraphQL `case`). */
export interface CaseDraftDetail {
  readonly id: string;
  readonly title: string;
  readonly status: CaseStatus;
  readonly statusLabel: string;
  readonly formDataRaw: string;
  readonly form: DraftFormModel;
  readonly updatedAt: string;
  readonly updatedAtLabel: string;
  readonly submittedAt: string | null;
  readonly submittedAtLabel: string | null;
  readonly canEdit: boolean;
}

export interface UpdateDraftCaseInput {
  readonly id: string;
  readonly title: string;
  readonly formData: string;
}

export interface SubmitCaseInput {
  readonly id: string;
}

/** User-facing case list / detail load failure. */
export class CasesLoadError extends Error {
  readonly code?: string;

  constructor(message: string, code?: string) {
    super(message);
    this.name = 'CasesLoadError';
    this.code = code;
  }
}

/** User-facing create-draft failure. */
export class CreateDraftError extends Error {
  readonly code?: string;

  constructor(message: string, code?: string) {
    super(message);
    this.name = 'CreateDraftError';
    this.code = code;
  }
}

/** User-facing save-draft / submit failure. */
export class DraftActionError extends Error {
  readonly code?: string;

  constructor(message: string, code?: string) {
    super(message);
    this.name = 'DraftActionError';
    this.code = code;
  }
}

/** Wire shape for GraphQL `cases` HTTP body. */
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
        updatedAt?: string;
      } | null> | null;
    } | null;
  };
  errors?: GraphqlError[];
}

/** Wire shape for GraphQL `createDraftCase`. */
export interface GraphqlCreateDraftBody {
  data?: {
    createDraftCase?: {
      id?: string;
      title?: string;
      status?: string;
      updatedAt?: string;
    } | null;
  };
  errors?: GraphqlError[];
}

/** Wire shape for GraphQL `case` detail. */
export interface GraphqlCaseDetailBody {
  data?: {
    case?: {
      case?: {
        id?: string;
        title?: string;
        status?: string;
        formData?: string;
        updatedAt?: string;
        submittedAt?: string | null;
      } | null;
    } | null;
  };
  errors?: GraphqlError[];
}

/** Wire shape for GraphQL `updateDraftCase`. */
export interface GraphqlUpdateDraftBody {
  data?: {
    updateDraftCase?: {
      id?: string;
      title?: string;
      status?: string;
      formData?: string;
      updatedAt?: string;
      submittedAt?: string | null;
    } | null;
  };
  errors?: GraphqlError[];
}

/** Wire shape for GraphQL `submitCase`. */
export interface GraphqlSubmitCaseBody {
  data?: {
    submitCase?: {
      id?: string;
      status?: string;
      submittedAt?: string | null;
      updatedAt?: string;
    } | null;
  };
  errors?: GraphqlError[];
}
