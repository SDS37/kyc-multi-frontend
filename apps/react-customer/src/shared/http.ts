import { appConfig } from '../config/app-config';
import type { GraphqlResponse } from './graphql.models';
import { tokenStorage } from '../auth/token-storage';

export interface GraphqlRequestOptions {
  /** When true, do not attach Bearer (login / registerTenant). */
  readonly skipAuth?: boolean;
}

/**
 * Typed GraphQL POST helper (KYC-070).
 * Attaches Authorization when a session token exists unless skipAuth is set.
 */
export async function graphqlRequest<TData>(
  query: string,
  variables?: Record<string, unknown>,
  options: GraphqlRequestOptions = {},
): Promise<GraphqlResponse<TData>> {
  const headers: Headers = new Headers({
    'Content-Type': 'application/json',
  });

  if (!options.skipAuth) {
    const token: string | null = tokenStorage.getAccessToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
  }

  const response: Response = await fetch(appConfig.graphqlUrl, {
    method: 'POST',
    headers,
    body: JSON.stringify({ query, variables }),
  });

  if (!response.ok) {
    throw new Error(`GraphQL HTTP ${String(response.status)}`);
  }

  const body: GraphqlResponse<TData> = (await response.json()) as GraphqlResponse<TData>;
  return body;
}

/** REST helper under apiBaseUrl with the same JWT attachment rules. */
export async function apiFetch(
  path: string,
  init: RequestInit = {},
  options: GraphqlRequestOptions = {},
): Promise<Response> {
  const headers = new Headers(init.headers);
  if (!options.skipAuth) {
    const token: string | null = tokenStorage.getAccessToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
  }

  const url: string = path.startsWith('http')
    ? path
    : `${appConfig.apiBaseUrl.replace(/\/$/, '')}/${path.replace(/^\//, '')}`;

  return fetch(url, { ...init, headers });
}
