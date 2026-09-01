import type { AppConfig } from './config.models';

function nonEmptyEnv(value: string | undefined, fallback: string): string {
  if (typeof value === 'string' && value.trim().length > 0) {
    return value.trim();
  }
  return fallback;
}

/**
 * Runtime config from Vite env (KYC-080). Defaults target local API.
 * Vite only statically inlines literal `import.meta.env.VITE_*` reads.
 */
export const appConfig: AppConfig = {
  apiBaseUrl: nonEmptyEnv(import.meta.env.VITE_API_BASE_URL, 'http://localhost:5295'),
  graphqlUrl: nonEmptyEnv(
    import.meta.env.VITE_GRAPHQL_URL,
    'http://localhost:5295/graphql',
  ),
};
