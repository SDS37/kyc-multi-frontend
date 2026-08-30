import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { APP_CONFIG, AppConfig } from '../config/app-config';
import {
  parseCaseActionStatus,
  parseCaseDetail,
  parseCasesPage,
  toCaseActionError,
  toCaseDownloadError,
  toCasesLoadError,
  toDocumentDownloadUrl,
  toListCasesVariables,
} from './cases.mappers';
import {
  CaseActionError,
  CaseDetail,
  CaseDownloadError,
  CaseListPage,
  CaseStatus,
  CasesLoadError,
  GraphqlCaseActionBody,
  GraphqlCaseDetailBody,
  GraphqlCasesBody,
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
        customerEmail
        updatedAt
      }
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
        customerUserId
        customerEmail
        createdAt
        updatedAt
        submittedAt
        reviewedAt
        reviewedBy
        reviewComment
      }
      comments {
        text
        createdAt
        authorUserId
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

const START_REVIEW_MUTATION: string = `
  mutation StartCaseReview($input: StartCaseReviewRequestInput!) {
    startCaseReview(input: $input) {
      id
      status
      updatedAt
    }
  }
`;

const APPROVE_MUTATION: string = `
  mutation ApproveCase($input: ApproveCaseRequestInput!) {
    approveCase(input: $input) {
      id
      status
      reviewedAt
      reviewedBy
      reviewComment
    }
  }
`;

const REJECT_MUTATION: string = `
  mutation RejectCase($input: RejectCaseRequestInput!) {
    rejectCase(input: $input) {
      id
      status
      reviewedAt
      reviewedBy
      reviewComment
    }
  }
`;

/**
 * Authenticated GraphQL cases API + REST document download (KYC-062 / KYC-063).
 * Mapping is pure (`cases.mappers`); this class only performs HTTP.
 */
@Injectable({ providedIn: 'root' })
export class CasesService {
  private readonly http: HttpClient = inject(HttpClient);
  private readonly config: AppConfig = inject(APP_CONFIG);

  list(params: ListCasesParams = {}): Observable<CaseListPage> {
    const variables = toListCasesVariables(params);

    return this.http
      .post<GraphqlCasesBody>(this.config.graphqlUrl, {
        query: LIST_CASES_QUERY,
        variables,
      })
      .pipe(
        map((body: GraphqlCasesBody): CaseListPage => parseCasesPage(body, variables)),
        catchError((err: unknown): Observable<never> =>
          throwError((): CasesLoadError => toCasesLoadError(err)),
        ),
      );
  }

  getById(caseId: string): Observable<CaseDetail> {
    return this.http
      .post<GraphqlCaseDetailBody>(this.config.graphqlUrl, {
        query: CASE_DETAIL_QUERY,
        variables: { id: caseId },
      })
      .pipe(
        map((body: GraphqlCaseDetailBody): CaseDetail => parseCaseDetail(body)),
        catchError((err: unknown): Observable<never> =>
          throwError((): CasesLoadError => toCasesLoadError(err)),
        ),
      );
  }

  startReview(caseId: string): Observable<CaseStatus> {
    return this.http
      .post<GraphqlCaseActionBody>(this.config.graphqlUrl, {
        query: START_REVIEW_MUTATION,
        variables: { input: { id: caseId } },
      })
      .pipe(
        map(
          (body: GraphqlCaseActionBody): CaseStatus =>
            parseCaseActionStatus(body, 'startCaseReview'),
        ),
        catchError((err: unknown): Observable<never> =>
          throwError((): CaseActionError => toCaseActionError(err)),
        ),
      );
  }

  approve(caseId: string, comment: string | null): Observable<CaseStatus> {
    return this.http
      .post<GraphqlCaseActionBody>(this.config.graphqlUrl, {
        query: APPROVE_MUTATION,
        variables: { input: { id: caseId, comment } },
      })
      .pipe(
        map(
          (body: GraphqlCaseActionBody): CaseStatus =>
            parseCaseActionStatus(body, 'approveCase'),
        ),
        catchError((err: unknown): Observable<never> =>
          throwError((): CaseActionError => toCaseActionError(err)),
        ),
      );
  }

  reject(caseId: string, comment: string): Observable<CaseStatus> {
    return this.http
      .post<GraphqlCaseActionBody>(this.config.graphqlUrl, {
        query: REJECT_MUTATION,
        variables: { input: { id: caseId, comment } },
      })
      .pipe(
        map(
          (body: GraphqlCaseActionBody): CaseStatus =>
            parseCaseActionStatus(body, 'rejectCase'),
        ),
        catchError((err: unknown): Observable<never> =>
          throwError((): CaseActionError => toCaseActionError(err)),
        ),
      );
  }

  downloadDocument(caseId: string, documentId: string): Observable<Blob> {
    const url: string = toDocumentDownloadUrl(this.config.apiBaseUrl, caseId, documentId);
    return this.http.get(url, { responseType: 'blob' }).pipe(
      catchError((err: unknown): Observable<never> =>
        throwError((): CaseDownloadError => toCaseDownloadError(err)),
      ),
    );
  }
}
