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

/** User-facing case list failure. */
export class CasesLoadError extends Error {
  constructor(
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'CasesLoadError';
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
