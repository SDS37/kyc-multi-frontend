import { AppConfig } from '../app/config/config.models';

/**
 * Production defaults (KYC-095). `ng build` uses this file.
 * Local `ng serve` file-replaces with `environment.development.ts`.
 * Set real HTTPS origins before a production deploy — empty/localhost fail at bootstrap.
 */
export const environment: AppConfig & { readonly production: boolean } = {
  production: true,
  apiBaseUrl: '',
  graphqlUrl: '',
  captchaRequiredForLogin: false,
  turnstileSiteKey: '',
};
