import { createBrowserRouter, Navigate, type DataRouter } from 'react-router';
import { LoginPage } from './auth/login-page/login-page';
import { RequireAuth, RequireGuest } from './auth/route-guards';
import { CaseDraftPlaceholder } from './cases/case-draft-placeholder/case-draft-placeholder';
import { CaseList } from './cases/case-list/case-list';
import { CustomerShell } from './layout/customer-shell';

/** App route table (KYC-072). Login outside shell; my cases under auth. */
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
        element: <CaseList />,
      },
      {
        path: 'cases/:caseId',
        element: <CaseDraftPlaceholder />,
      },
    ],
  },
  {
    path: '*',
    element: <Navigate to="/cases" replace />,
  },
]);
