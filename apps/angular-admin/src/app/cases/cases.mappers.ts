import { HttpErrorResponse } from '@angular/common/http';
import { GraphqlError } from '../shared/graphql.models';
import {
  CaseListItem,
  CaseListPage,
  CaseStatus,
  CasesLoadError,
  GraphqlCasesBody,
  ListCasesParams,
  isCaseStatus,
} from './cases.models';

export interface ListCasesVariables {
  status: CaseStatus | null;
  skip: number;
  take: number;
}

/** Pure: build GraphQL variables from list params. */
export function toListCasesVariables(params: ListCasesParams = {}): ListCasesVariables {
  return {
    status: params.status ?? null,
    skip: params.skip ?? 0,
    take: params.take ?? 20,
  };
}

/** Pure: map GraphQL `cases` body → page DTO (throws CasesLoadError on bad payload). */
export function parseCasesPage(
  body: GraphqlCasesBody,
  requested: ListCasesVariables,
): CaseListPage {
  const gqlError: GraphqlError | undefined = body.errors?.[0];
  if (gqlError) {
    throw new CasesLoadError(
      gqlError.message?.trim() || 'Unable to load cases. Try again.',
      gqlError.extensions?.code,
    );
  }

  const page = body.data?.cases;
  if (!page || !Array.isArray(page.items)) {
    throw new CasesLoadError('Unable to load cases. Try again.');
  }

  const items: CaseListItem[] = page.items.map(
    (raw): CaseListItem => {
      if (!raw?.id || !raw.title || !raw.customerEmail || !raw.updatedAt || !raw.status) {
        throw new CasesLoadError('Case list response was incomplete.');
      }
      if (!isCaseStatus(raw.status)) {
        throw new CasesLoadError(`Unexpected case status: ${raw.status}`);
      }
      return {
        id: raw.id,
        title: raw.title,
        status: raw.status,
        customerEmail: raw.customerEmail,
        updatedAt: raw.updatedAt,
      };
    },
  );

  return {
    items,
    totalCount: page.totalCount ?? items.length,
    skip: page.skip ?? requested.skip,
    take: page.take ?? requested.take,
  };
}

/** Pure: normalize unknown filter control values. */
export function parseStatusFilterValue(value: unknown): CaseStatus | null | undefined {
  if (value === null) {
    return null;
  }
  if (typeof value === 'string' && isCaseStatus(value)) {
    return value;
  }
  return undefined;
}

/** Pure: map transport / unknown errors to CasesLoadError. */
export function toCasesLoadError(err: unknown): CasesLoadError {
  if (err instanceof CasesLoadError) {
    return err;
  }
  if (err instanceof HttpErrorResponse) {
    return new CasesLoadError(
      'Unable to reach the cases service. Try again in a moment.',
      'NETWORK',
    );
  }
  return new CasesLoadError('Unable to load cases. Try again.');
}
