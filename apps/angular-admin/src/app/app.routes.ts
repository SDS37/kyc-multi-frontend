import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './auth/auth.guard';
import type { Login } from './auth/login/login';
import type { CaseList } from './cases/case-list/case-list';

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
    path: 'cases',
    canActivate: [authGuard],
    loadComponent: (): Promise<typeof CaseList> =>
      import('./cases/case-list/case-list').then(
        (m: { CaseList: typeof CaseList }): typeof CaseList => m.CaseList,
      ),
  },
  { path: '', pathMatch: 'full', redirectTo: 'cases' },
  { path: '**', redirectTo: 'cases' },
];
