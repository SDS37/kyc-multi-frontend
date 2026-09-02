import { appConfig } from '../config/app-config';
import type { GraphqlError, GraphqlResponse } from './graphql.models';
import { GraphqlHttpError } from './graphql.models';
import { notifySessionCleared } from '../auth/session-events';
import { tokenStorage } from '../auth/token-storage';

/** Auth attachment options for GraphQL and REST helpers. */
export interface ApiAuthOptions {
  /** When true, do not attach Bearer (login / registerTenant). */
  readonly skipAuth?: boolean;
}

/**
 * Typed GraphQL POST helper (KYC-070).
 * Attaches Authorization when a session token exists unless skipAuth is set.
 * Clears the session on HTTP 401 or GraphQL AUTH_NOT_AUTHENTICATED.
 */
export async function graphqlRequest<TData>(
  query: string,
  variables?: Record<string, unknown>,
  options: ApiAuthOptions = {},
): Promise<GraphqlResponse<TData>> {
  const headers: Headers = new Headers({
    'Content-Type': 'application/json',
  });

  if (!options.skipAuth && isConfiguredApiUrl(appConfig.graphqlUrl)) {
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

  clearSessionOnUnauthorized(response);

  if (!response.ok) {
    throw new GraphqlHttpError(response.status);
  }

  const raw: unknown = await response.json();
  const parsed: GraphqlResponse<TData> = parseGraphqlResponse<TData>(raw);
  clearSessionOnGraphqlAuthFailure(parsed.errors);
  return parsed;
}

/** REST helper under apiBaseUrl with the same JWT attachment rules. */
export async function apiFetch(
  path: string,
  init: RequestInit = {},
  options: ApiAuthOptions = {},
): Promise<Response> {
  const headers: Headers = new Headers(init.headers);
  const url: string = path.startsWith('http')
    ? path
    : `${appConfig.apiBaseUrl.replace(/\/$/, '')}/${path.replace(/^\//, '')}`;

  if (!options.skipAuth && isConfiguredApiUrl(url)) {
    const token: string | null = tokenStorage.getAccessToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
  }

  const response: Response = await fetch(url, { ...init, headers });
  clearSessionOnUnauthorized(response);
  return response;
}

/** True when the URL is under the configured API origin (Angular interceptor parity). */
export function isConfiguredApiUrl(requestUrl: string): boolean {
  const base: string = appConfig.apiBaseUrl.replace(/\/$/, '');
  try {
    const absolute: URL = new URL(requestUrl, typeof window !== 'undefined' ? window.location.origin : base);
    return absolute.href === base || absolute.href.startsWith(`${base}/`);
  } catch {
    return false;
  }
}

function clearSessionOnUnauthorized(response: Response): void {
  if (response.status === 401) {
    tokenStorage.clearSession();
    notifySessionCleared();
  }
}

function clearSessionOnGraphqlAuthFailure(errors: GraphqlError[] | undefined): void {
  if (!errors?.length) {
    return;
  }
  const expired: boolean = errors.some(
    (err: GraphqlError): boolean => err.extensions?.code === 'AUTH_NOT_AUTHENTICATED',
  );
  if (expired) {
    tokenStorage.clearSession();
    notifySessionCleared();
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function parseGraphqlResponse<TData>(value: unknown): GraphqlResponse<TData> {
  if (!isRecord(value)) {
    throw new Error('Invalid GraphQL response body');
  }

  const data: TData | null | undefined =
    'data' in value ? (value['data'] as TData | null | undefined) : undefined;

  const errorsRaw: unknown = value['errors'];
  const errors: GraphqlError[] | undefined = Array.isArray(errorsRaw)
    ? errorsRaw.filter(isRecord).map(toGraphqlError)
    : undefined;

  return { data, errors };
}

function toGraphqlError(value: Record<string, unknown>): GraphqlError {
  const messageRaw: unknown = value['message'];
  const message: string | undefined =
    typeof messageRaw === 'string' ? messageRaw : undefined;

  const extensionsRaw: unknown = value['extensions'];
  let extensions: GraphqlError['extensions'];
  if (isRecord(extensionsRaw)) {
    const codeRaw: unknown = extensionsRaw['code'];
    const code: string | undefined =
      typeof codeRaw === 'string' ? codeRaw : undefined;
    extensions = code === undefined ? undefined : { code };
  }

  return { message, extensions };
}
