/** Cross-feature GraphQL error wire shape. */
export interface GraphqlError {
  message?: string;
  extensions?: {
    code?: string;
  };
}

export interface GraphqlResponse<TData> {
  data?: TData | null;
  errors?: GraphqlError[];
}

/** Transport failure from a GraphQL HTTP POST (non-2xx). */
export class GraphqlHttpError extends Error {
  readonly status: number;

  constructor(status: number) {
    super(`GraphQL HTTP ${String(status)}`);
    this.name = 'GraphqlHttpError';
    this.status = status;
  }
}
