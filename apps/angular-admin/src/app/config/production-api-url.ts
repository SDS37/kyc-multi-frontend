/** Fail fast when a production Angular build would call localhost (KYC-095). */

export function isLocalhostApiUrl(url: string): boolean {
  const trimmed: string = url.trim();
  if (trimmed.length === 0) {
    return false;
  }
  try {
    const parsed: URL = new URL(trimmed);
    const host: string = parsed.hostname.toLowerCase();
    return host === 'localhost' || host === '127.0.0.1' || host === '[::1]' || host === '::1';
  } catch {
    return /localhost|127\.0\.0\.1|\[::1\]/i.test(trimmed);
  }
}

export function assertProductionApiConfig(env: {
  readonly production: boolean;
  readonly apiBaseUrl: string;
  readonly graphqlUrl: string;
}): void {
  if (!env.production) {
    return;
  }

  const apiBaseUrl: string = env.apiBaseUrl.trim();
  const graphqlUrl: string = env.graphqlUrl.trim();
  if (apiBaseUrl.length === 0 || graphqlUrl.length === 0) {
    throw new Error(
      'Production apiBaseUrl and graphqlUrl must be set. Do not ship an empty or localhost API URL.',
    );
  }
  if (isLocalhostApiUrl(apiBaseUrl) || isLocalhostApiUrl(graphqlUrl)) {
    throw new Error(
      'Production apiBaseUrl and graphqlUrl must not point at localhost. Set an explicit deployed API origin.',
    );
  }
}
