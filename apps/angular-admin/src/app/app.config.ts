import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth/auth.interceptor';
import { APP_CONFIG } from './config/app-config';
import { routes } from './app.routes';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    {
      provide: APP_CONFIG,
      useValue: {
        apiBaseUrl: environment.apiBaseUrl,
        graphqlUrl: environment.graphqlUrl,
        captchaRequiredForLogin: environment.captchaRequiredForLogin,
        turnstileSiteKey: environment.turnstileSiteKey,
      },
    },
  ],
};
