import { AppConfig } from '../app/config/config.models';

export const environment: AppConfig & { readonly production: boolean } = {
  production: false,
  apiBaseUrl: 'http://localhost:5295',
  graphqlUrl: 'http://localhost:5295/graphql',
};
