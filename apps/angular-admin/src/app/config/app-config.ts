import { InjectionToken } from '@angular/core';
import { AppConfig } from './config.models';

export type { AppConfig } from './config.models';

export const APP_CONFIG: InjectionToken<AppConfig> = new InjectionToken<AppConfig>('APP_CONFIG');
