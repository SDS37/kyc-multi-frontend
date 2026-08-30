import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './auth/auth.guard';
import type { Login } from './auth/login/login';
import type { CaseList } from './cases/case-list/case-list';
import type { CaseReview } from './cases/case-review/case-review';
import type { AdminShell } from './layout/admin-shell/admin-shell';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: (): Promise<typeof Login> =>
      import('./auth/login/login').then(
        (m: { Login: typeof Login }): typeof Login => m.Login,
      ),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: (): Promise<typeof AdminShell> =>
      import('./layout/admin-shell/admin-shell').then(
        (m: { AdminShell: typeof AdminShell }): typeof AdminShell => m.AdminShell,
      ),
    children: [
      {
        path: 'cases',
        loadComponent: (): Promise<typeof CaseList> =>
          import('./cases/case-list/case-list').then(
            (m: { CaseList: typeof CaseList }): typeof CaseList => m.CaseList,
          ),
      },
      {
        path: 'cases/:caseId',
        loadComponent: (): Promise<typeof CaseReview> =>
          import('./cases/case-review/case-review').then(
            (m: { CaseReview: typeof CaseReview }): typeof CaseReview => m.CaseReview,
          ),
      },
      { path: '', pathMatch: 'full', redirectTo: 'cases' },
    ],
  },
  { path: '**', redirectTo: 'cases' },
];
