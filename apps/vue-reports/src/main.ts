import { createApp, type App as VueApp } from 'vue';
import App from './App.vue';
import { appRouter } from './app-router';
import { appConfig } from './config/app-config';
import { assertProductionApiConfig } from './config/production-api-url';
import './styles.css';

/** Reject an empty or localhost API origin before the first screen (KYC-112). */
assertProductionApiConfig({
  production: import.meta.env.PROD,
  apiBaseUrl: appConfig.apiBaseUrl,
  graphqlUrl: appConfig.graphqlUrl,
});

const app: VueApp = createApp(App);
app.use(appRouter);
app.mount('#app');
