import { graphqlRequest } from '../shared/http';
import {
  parseCasesPage,
  parseCreatedDraft,
  toCasesLoadError,
  toCreateDraftError,
  toCreateDraftVariables,
  toListCasesVariables,
} from './cases.mappers';
import type {
  CaseListPage,
  CreateDraftCaseInput,
  CreatedDraftCase,
  ListCasesParams,
} from './cases.models';

const LIST_CASES_QUERY: string = `
  query Cases($status: CaseStatus, $skip: Int, $take: Int) {
    cases(status: $status, skip: $skip, take: $take) {
      totalCount
      skip
      take
      items {
        id
        title
        status
        updatedAt
      }
    }
  }
`;

const CREATE_DRAFT_MUTATION: string = `
  mutation CreateDraftCase($input: CreateDraftCaseRequestInput!) {
    createDraftCase(input: $input) {
      id
      title
      status
      updatedAt
    }
  }
`;

/**
 * Authenticated cases GraphQL (KYC-072).
 * Never use skipAuth — JWT ownership is enforced by the API.
 */
export async function listCases(params: ListCasesParams = {}): Promise<CaseListPage> {
  const variables = toListCasesVariables(params);
  try {
    const body = await graphqlRequest<{
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
    }>(LIST_CASES_QUERY, { ...variables });
    return parseCasesPage(body, variables);
  } catch (err: unknown) {
    throw toCasesLoadError(err);
  }
}

/**
 * Customer-only createDraftCase.
 * Input is title only — never send tenantId / customerUserId (ADR-007).
 */
export async function createDraftCase(
  input: CreateDraftCaseInput,
): Promise<CreatedDraftCase> {
  const variables = toCreateDraftVariables(input);
  try {
    const body = await graphqlRequest<{
      createDraftCase?: {
        id?: string;
        title?: string;
        status?: string;
        updatedAt?: string;
      } | null;
    }>(CREATE_DRAFT_MUTATION, variables);
    return parseCreatedDraft(body);
  } catch (err: unknown) {
    throw toCreateDraftError(err);
  }
}
