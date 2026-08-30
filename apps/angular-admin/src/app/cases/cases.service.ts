import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { APP_CONFIG, AppConfig } from '../config/app-config';
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
 */
@Injectable({ providedIn: 'root' })
export class CasesService {
  private readonly http: HttpClient = inject(HttpClient);
  private readonly config: AppConfig = inject(APP_CONFIG);

  list(params: ListCasesParams = {}): Observable<CaseListPage> {
    const variables: {
      status: CaseStatus | null;
      skip: number;
      take: number;
    } = {
      status: params.status ?? null,
      skip: params.skip ?? 0,
      take: params.take ?? 20,
    };

    return this.http
      .post<GraphqlCasesBody>(this.config.graphqlUrl, {
        query: LIST_CASES_QUERY,
        variables,
      })
      .pipe(
        map((body: GraphqlCasesBody): CaseListPage => {
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

          const items: CaseListItem[] = [];
          for (const raw of page.items) {
            if (!raw?.id || !raw.title || !raw.customerEmail || !raw.updatedAt || !raw.status) {
              throw new CasesLoadError('Case list response was incomplete.');
            }
            if (!isCaseStatus(raw.status)) {
              throw new CasesLoadError(`Unexpected case status: ${raw.status}`);
            }
            items.push({
              id: raw.id,
              title: raw.title,
              status: raw.status,
              customerEmail: raw.customerEmail,
              updatedAt: raw.updatedAt,
            });
          }

          return {
            items,
            totalCount: page.totalCount ?? items.length,
            skip: page.skip ?? variables.skip,
            take: page.take ?? variables.take,
          };
        }),
        catchError((err: unknown): Observable<never> => {
          if (err instanceof CasesLoadError) {
            return throwError((): CasesLoadError => err);
          }
          if (err instanceof HttpErrorResponse) {
            return throwError(
              (): CasesLoadError =>
                new CasesLoadError(
                  'Unable to reach the cases service. Try again in a moment.',
                  'NETWORK',
                ),
            );
          }
          return throwError(
            (): CasesLoadError => new CasesLoadError('Unable to load cases. Try again.'),
          );
        }),
      );
  }
}
