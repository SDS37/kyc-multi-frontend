import { createBrowserRouter, Navigate, type DataRouter } from 'react-router';
import { LoginPage } from './auth/login-page/login-page';
import { RequireAuth, RequireGuest } from './auth/route-guards';
import { CaseDraft } from './cases/case-draft/case-draft';
import { CaseList } from './cases/case-list/case-list';
import { CustomerShell } from './layout/customer-shell';

/** App route table (KYC-072 / KYC-073). Login outside shell; cases under auth. */
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
        element: <CaseDraft />,
      },
    ],
  },
  {
    path: '*',
    element: <Navigate to="/cases" replace />,
  },
]);
