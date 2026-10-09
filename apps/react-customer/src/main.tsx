import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { appConfig } from './config/app-config';
import { assertProductionApiConfig } from './config/production-api-url';
import './styles.css';

/** Reject an empty or localhost API origin before the first screen (KYC-112). */
assertProductionApiConfig({
  production: import.meta.env.PROD,
  apiBaseUrl: appConfig.apiBaseUrl,
  graphqlUrl: appConfig.graphqlUrl,
});

const rootElement: HTMLElement | null = document.getElementById('root');
if (!rootElement) {
  throw new Error('Root element #root not found');
}

createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
