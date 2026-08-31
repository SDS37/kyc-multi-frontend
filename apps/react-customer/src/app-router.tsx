import { createBrowserRouter } from 'react-router';
import { CustomerShell } from './layout/customer-shell';
import { HomePlaceholder } from './routes/home-placeholder';

/** App route table (KYC-070). Login + cases land in later stories. */
export const appRouter = createBrowserRouter([
  {
    path: '/',
    element: <CustomerShell />,
    children: [
      {
        index: true,
        element: <HomePlaceholder />,
      },
    ],
  },
]);
