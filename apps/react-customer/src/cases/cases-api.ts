import { graphqlRequest } from '../shared/http';
import {
  draftFormToFormDataJson,
  parseCaseDraftDetail,
  parseCasesPage,
  parseCreatedDraft,
  parseSubmittedCase,
  parseUpdatedDraft,
  toCaseDetailLoadError,
  toCasesLoadError,
  toCreateDraftError,
  toCreateDraftVariables,
  toDraftActionError,
  toListCasesVariables,
  toSubmitCaseVariables,
  toUpdateDraftVariables,
} from './cases.mappers';
import type {
  CaseDraftDetail,
  CaseListPage,
  CreateDraftCaseInput,
  CreatedDraftCase,
  DraftFormModel,
  ListCasesParams,
  SubmitCaseInput,
  UpdateDraftCaseInput,
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

const CASE_DETAIL_QUERY: string = `
  query CaseDetail($id: UUID!) {
    case(id: $id) {
      case {
        id
        title
        status
        formData
        updatedAt
        submittedAt
      }
    }
  }
`;

const UPDATE_DRAFT_MUTATION: string = `
  mutation UpdateDraftCase($input: UpdateDraftCaseRequestInput!) {
    updateDraftCase(input: $input) {
      id
      title
      status
      formData
      updatedAt
      submittedAt
    }
  }
`;

const SUBMIT_CASE_MUTATION: string = `
  mutation SubmitCase($input: SubmitCaseRequestInput!) {
    submitCase(input: $input) {
      id
      status
      submittedAt
      updatedAt
    }
  }
`;

/**
 * Authenticated cases GraphQL (KYC-072 / KYC-073).
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

/** Load case detail for the draft editor. */
export async function getCaseDetail(caseId: string): Promise<CaseDraftDetail> {
  try {
    const body = await graphqlRequest<{
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
    }>(CASE_DETAIL_QUERY, { id: caseId });
    return parseCaseDraftDetail(body);
  } catch (err: unknown) {
    throw toCaseDetailLoadError(err);
  }
}

/** Persist title + FormData on a draft. */
export async function updateDraftCase(
  caseId: string,
  form: DraftFormModel,
): Promise<CaseDraftDetail> {
  const input: UpdateDraftCaseInput = {
    id: caseId,
    title: form.title,
    formData: draftFormToFormDataJson(form),
  };
  const variables = toUpdateDraftVariables(input);
  try {
    const body = await graphqlRequest<{
      updateDraftCase?: {
        id?: string;
        title?: string;
        status?: string;
        formData?: string;
        updatedAt?: string;
        submittedAt?: string | null;
      } | null;
    }>(UPDATE_DRAFT_MUTATION, variables);
    return parseUpdatedDraft(body);
  } catch (err: unknown) {
    throw toDraftActionError(err, 'save');
  }
}

/** Submit a draft (FormData must already be valid on the server). */
export async function submitCase(
  caseId: string,
  previous: CaseDraftDetail,
): Promise<CaseDraftDetail> {
  const input: SubmitCaseInput = { id: caseId };
  const variables = toSubmitCaseVariables(input);
  try {
    const body = await graphqlRequest<{
      submitCase?: {
        id?: string;
        status?: string;
        submittedAt?: string | null;
        updatedAt?: string;
      } | null;
    }>(SUBMIT_CASE_MUTATION, variables);
    return parseSubmittedCase(body, previous);
  } catch (err: unknown) {
    throw toDraftActionError(err, 'submit');
  }
}
