import { createBrowserRouter, Navigate, type DataRouter } from 'react-router';
import { LoginPage } from './auth/login-page/login-page';
import { RequireAuth, RequireGuest } from './auth/route-guards';
import { CasesPlaceholder } from './cases/cases-placeholder/cases-placeholder';
import { CustomerShell } from './layout/customer-shell';

/** App route table (KYC-071). Login outside shell; cases stub until KYC-072. */
export const appRouter: DataRouter = createBrowserRouter([
  {
    path: '/login',
    element: (
      <RequireGuest>
        <LoginPage />
      </RequireGuest>
    ),
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <CustomerShell />
      </RequireAuth>
    ),
    children: [
      {
        index: true,
        element: <Navigate to="/cases" replace />,
      },
      {
        path: 'cases',
        element: <CasesPlaceholder />,
      },
    ],
  },
  {
    path: '*',
    element: <Navigate to="/cases" replace />,
  },
]);
