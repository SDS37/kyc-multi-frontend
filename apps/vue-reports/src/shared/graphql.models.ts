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
