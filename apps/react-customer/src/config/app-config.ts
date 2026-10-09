import type { AppConfig } from './config.models';
import { resolveConfiguredUrl } from './production-api-url';

const DEV_API_BASE_URL: string = 'http://localhost:5295';
const DEV_GRAPHQL_URL: string = 'http://localhost:5295/graphql';

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
 * Runtime config from Vite env (KYC-070). Development defaults target the local API.
 * Production leaves an empty or localhost value in place. `main.tsx` rejects that before render (KYC-112).
 * Vite only statically inlines literal `import.meta.env.VITE_*` reads — never use dynamic keys.
 */
const production: boolean = import.meta.env.PROD;
const apiBaseUrl: string = resolveConfiguredUrl(
  import.meta.env.VITE_API_BASE_URL,
  DEV_API_BASE_URL,
  production,
);
const graphqlUrl: string = resolveConfiguredUrl(
  import.meta.env.VITE_GRAPHQL_URL,
  DEV_GRAPHQL_URL,
  production,
);

export const appConfig: AppConfig = {
  apiBaseUrl,
  graphqlUrl,
  captchaRequiredForLogin: envFlag(import.meta.env.VITE_CAPTCHA_REQUIRED_FOR_LOGIN),
  turnstileSiteKey: nonEmptyEnv(import.meta.env.VITE_TURNSTILE_SITE_KEY, ''),
};
