import { AppConfig } from '../app/config/app-config';

export const environment: AppConfig & { readonly production: boolean } = {
  production: true,
  /** .NET API origin (GraphQL + document REST). */
  apiBaseUrl: 'http://localhost:5295',
  /** Hot Chocolate endpoint used by the admin app. */
  graphqlUrl: 'http://localhost:5295/graphql',
};
