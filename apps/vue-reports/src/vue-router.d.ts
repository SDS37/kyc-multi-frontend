import 'vue-router';

declare module 'vue-router' {
  interface RouteMeta {
    readonly requiresAuth?: boolean;
    readonly guestOnly?: boolean;
    readonly title?: string;
  }
}
