import { RouterProvider } from 'react-router';
import type { ReactElement } from 'react';
import { appRouter } from './app-router';

/** Root composition: router only (KYC-070). */
export function App(): ReactElement {
  return <RouterProvider router={appRouter} />;
}
