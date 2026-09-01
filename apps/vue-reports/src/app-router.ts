import {
  createRouter,
  createWebHistory,
  type RouteLocationNormalized,
  type RouteRecordRaw,
  type Router,
} from 'vue-router';
import { resolveReportsNavigation } from './auth/auth.mappers';
import type { ReportsNavigationRedirect, ShellSession } from './auth/auth.models';
import { getValidShellSession } from './auth/session';
import { tokenStorage } from './auth/token-storage';
import LoginPage from './auth/login-page/LoginPage.vue';
import ReportsShell from './layout/ReportsShell.vue';
import ReportsHome from './reports/ReportsHome.vue';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: LoginPage,
    meta: { guestOnly: true, title: 'Sign in' },
  },
  {
    path: '/',
    component: ReportsShell,
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        redirect: '/reports',
      },
      {
        path: 'reports',
        name: 'reports',
        component: ReportsHome,
        meta: { requiresAuth: true, title: 'Reports' },
      },
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/reports',
  },
];

/** App route table (KYC-080). Login outside shell; reports under auth. */
export const appRouter: Router = createRouter({
  history: createWebHistory(),
  routes,
});

appRouter.beforeEach((to: RouteLocationNormalized) => {
  const session: ShellSession | null = getValidShellSession();
  const redirect: ReportsNavigationRedirect | null = resolveReportsNavigation(
    { fullPath: to.fullPath, meta: to.meta },
    session,
  );
  if (redirect === null) {
    return true;
  }
  if (redirect.clearSession) {
    tokenStorage.clearSession();
  }
  return {
    path: redirect.path,
    query: redirect.query,
    replace: redirect.replace,
  };
});

appRouter.afterEach((to: RouteLocationNormalized): void => {
  const title: unknown = to.meta['title'];
  if (typeof title === 'string' && title.trim()) {
    document.title = `KYC Reports · ${title}`;
  }
});
