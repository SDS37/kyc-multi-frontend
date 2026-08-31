import type { AppConfig } from './config.models';

function readEnv(name: string, fallback: string): string {
  const value: string | undefined = import.meta.env[name] as string | undefined;
  if (typeof value === 'string' && value.trim().length > 0) {
    return value.trim();
  }
  return fallback;
}

/** Runtime config from Vite env (KYC-070). Defaults target local API. */
export const appConfig: AppConfig = {
  apiBaseUrl: readEnv('VITE_API_BASE_URL', 'http://localhost:5295'),
  graphqlUrl: readEnv('VITE_GRAPHQL_URL', 'http://localhost:5295/graphql'),
};
