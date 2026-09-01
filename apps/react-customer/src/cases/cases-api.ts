import { apiFetch, graphqlRequest } from '../shared/http';
import {
  draftFormToFormDataJson,
  parseCaseDraftDetail,
  parseCasesPage,
  parseCreatedDraft,
  parseSubmittedCase,
  parseUpdatedDraft,
  parseUploadedDocument,
  toCaseDetailLoadError,
  toCasesLoadError,
  toCreateDraftError,
  toCreateDraftVariables,
  toDocumentUploadError,
  toDocumentUploadPath,
  toDocumentUploadTransportError,
  toDraftActionError,
  toListCasesVariables,
  toSubmitCaseVariables,
  toUpdateDraftVariables,
  validateDocumentFile,
} from './cases.mappers';
import type {
  CaseDocument,
  CaseDraftDetail,
  CaseListPage,
  CreateDraftCaseInput,
  CreatedDraftCase,
  DraftFormModel,
  ListCasesParams,
  SubmitCaseInput,
  UpdateDraftCaseInput,
} from './cases.models';
import { DocumentUploadError } from './cases.models';
import { CASES_DRAFT_MESSAGES } from './cases.messages';

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
      documents {
        id
        fileName
        contentType
        sizeBytes
        uploadedAt
        uploadedBy
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
 * Authenticated cases GraphQL + REST document upload (KYC-072–074).
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

/** Load case detail for the draft editor (includes document metadata). */
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
        documents?: Array<{
          id?: string;
          fileName?: string;
          contentType?: string;
          sizeBytes?: number;
          uploadedAt?: string;
          uploadedBy?: string;
        } | null> | null;
      } | null;
    }>(CASE_DETAIL_QUERY, { id: caseId });
    return parseCaseDraftDetail(body);
  } catch (err: unknown) {
    throw toCaseDetailLoadError(err);
  }
}

/** Persist title + FormData on a draft (preserves prior documents). */
export async function updateDraftCase(
  caseId: string,
  form: DraftFormModel,
  previous: CaseDraftDetail,
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
    return parseUpdatedDraft(body, previous);
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

/**
 * Customer upload via REST multipart (KYC-074 / ADR-002).
 * Field name must be `file`. Do not set Content-Type manually.
 */
export async function uploadDocument(
  caseId: string,
  file: File,
): Promise<CaseDocument> {
  const clientError: string | null = validateDocumentFile(file);
  if (clientError !== null) {
    throw new DocumentUploadError(clientError, 'VALIDATION');
  }

  const body: FormData = new FormData();
  body.append('file', file);

  try {
    const response: Response = await apiFetch(toDocumentUploadPath(caseId), {
      method: 'POST',
      body,
    });
    if (!response.ok) {
      throw await toDocumentUploadError(response);
    }
    const raw: unknown = await response.json();
    try {
      return parseUploadedDocument(raw);
    } catch (err: unknown) {
      if (err instanceof DocumentUploadError) {
        throw err;
      }
      throw new DocumentUploadError(CASES_DRAFT_MESSAGES.docsUploadIncomplete, 'INCOMPLETE');
    }
  } catch (err: unknown) {
    throw toDocumentUploadTransportError(err);
  }
}
