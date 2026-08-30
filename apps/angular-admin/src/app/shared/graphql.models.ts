/** Shared GraphQL error envelope fields (Hot Chocolate). */
export interface GraphqlError {
  message?: string;
  extensions?: { code?: string };
}
