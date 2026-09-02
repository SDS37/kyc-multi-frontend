import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { assertProductionApiConfig } from './app/config/production-api-url';
import { App } from './app/app';
import { environment } from './environments/environment';

assertProductionApiConfig(environment);

bootstrapApplication(App, appConfig).catch((err: unknown): void => {
  console.error(err);
});
