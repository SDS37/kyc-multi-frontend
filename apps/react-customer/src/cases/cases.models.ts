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

/** Max title length for `createDraftCase` (API validation). */
export const CREATE_DRAFT_TITLE_MAX_LENGTH: number = 200;

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

/** User-facing case list load failure. */
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
