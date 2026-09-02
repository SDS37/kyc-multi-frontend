/** Runtime API endpoints and optional login captcha (from Angular environments). */
export interface AppConfig {
  readonly apiBaseUrl: string;
  readonly graphqlUrl: string;
  /** Mirrors API `Captcha:RequiredForLogin`. Default off so Development login stays unchanged. */
  readonly captchaRequiredForLogin?: boolean;
  /** Cloudflare Turnstile site key. Empty → labeled token field when captcha is required. */
  readonly turnstileSiteKey?: string;
}
