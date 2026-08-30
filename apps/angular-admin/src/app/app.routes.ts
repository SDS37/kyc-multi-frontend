import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./auth/login/login').then((m) => m.Login),
  },
  {
    path: 'cases',
    canActivate: [authGuard],
    loadComponent: () => import('./cases/case-list/case-list').then((m) => m.CaseList),
  },
  { path: '', pathMatch: 'full', redirectTo: 'cases' },
  { path: '**', redirectTo: 'cases' },
];
