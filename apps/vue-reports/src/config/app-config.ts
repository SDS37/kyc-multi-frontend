import type { AppConfig } from './config.models';

function nonEmptyEnv(value: string | undefined, fallback: string): string {
  if (typeof value === 'string' && value.trim().length > 0) {
    return value.trim();
  }
  return fallback;
}

function envFlag(value: string | undefined): boolean {
  return value?.trim().toLowerCase() === 'true';
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
  captchaRequiredForLogin: envFlag(import.meta.env.VITE_CAPTCHA_REQUIRED_FOR_LOGIN),
  turnstileSiteKey: nonEmptyEnv(import.meta.env.VITE_TURNSTILE_SITE_KEY, ''),
};
