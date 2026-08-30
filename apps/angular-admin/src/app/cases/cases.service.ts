import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { APP_CONFIG, AppConfig } from '../config/app-config';
import {
  parseCasesPage,
  toCasesLoadError,
  toListCasesVariables,
} from './cases.mappers';
import {
  CaseListPage,
  CasesLoadError,
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

/**
 * Authenticated GraphQL `cases` list (KYC-062 / KYC-036).
 * JWT attached by authInterceptor — do not use SKIP_AUTH here.
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
}
