import type { GraphqlResponse } from '../shared/graphql.models';
import * as http from '../shared/http';
import { parseReportsOverview, toReportsLoadError } from './reports.mappers';
import type { GraphqlReportsOverviewData, ReportsOverview } from './reports.models';

/**
 * Aliased `cases` query (KYC-081).
 * Count aliases use take: 1 because ListCasesService rejects take < 1.
 * Latest uses take: 10 (must match LATEST_CASES_TAKE); API order is newest Id first (KYC-036).
 */
const REPORTS_OVERVIEW_QUERY: string = `
  query ReportsOverview {
    draft: cases(status: DRAFT, skip: 0, take: 1) {
      totalCount
    }
    submitted: cases(status: SUBMITTED, skip: 0, take: 1) {
      totalCount
    }
    inReview: cases(status: IN_REVIEW, skip: 0, take: 1) {
      totalCount
    }
    approved: cases(status: APPROVED, skip: 0, take: 1) {
      totalCount
    }
    rejected: cases(status: REJECTED, skip: 0, take: 1) {
      totalCount
    }
    latest: cases(skip: 0, take: 10) {
      totalCount
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
 * Load status counts + latest cases.
 * Auth is attached by graphqlRequest; session clear on 401 is HTTP-layer.
 */
export async function loadReportsOverview(): Promise<ReportsOverview> {
  try {
    const body: GraphqlResponse<GraphqlReportsOverviewData> =
      await http.graphqlRequest<GraphqlReportsOverviewData>(REPORTS_OVERVIEW_QUERY);
    return parseReportsOverview(body);
  } catch (err: unknown) {
    throw toReportsLoadError(err);
  }
}
