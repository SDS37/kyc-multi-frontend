import { InjectionToken } from '@angular/core';

/** Runtime API endpoints (from Angular environments). */
export interface AppConfig {
  readonly apiBaseUrl: string;
  readonly graphqlUrl: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
