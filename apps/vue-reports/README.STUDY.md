# Study: `apps/vue-reports`

**Aligned with:** `feat/kyc-080-vue-app-foundation` / KYC-080 (login + shell; table is KYC-081).

## Purpose

Read-only reports portal for **Reviewer / TenantAdmin** (ADR-004). KYC-080 is the shell, auth, and GraphQL client so KYC-081 can add counts / latest cases without scaffolding.

## Map

| Concern | Where |
|---|---|
| Smart login screen | `src/auth/login-page/LoginPage.vue` |
| Authenticated chrome | `src/layout/ReportsShell.vue` |
| Presentational home | `src/reports/ReportsHome.vue` |
| GraphQL | `src/shared/http.ts` + `src/auth/login-api.ts` |
| Pure parse / guards | `src/auth/auth.mappers.ts` (`resolveReportsNavigation`) |
| Router | `src/app-router.ts` — `beforeEach` **returns** a location (no `next()`) |

## Angular / React differences

Admin list is a review queue with mutations. Customer owns draft/upload. Vue is **read-only reports** — no approve, no document upload. Same `login` mutation; role filter is stricter (Customer blocked).

## Links

- [README.md](README.md)
- [Issue #36](https://github.com/SDS37/kyc-multi-frontend/issues/36) (KYC-080)
- [Vue Style Guide](https://vuejs.org/style-guide/)
- [roadmap time-control](../../docs/roadmap.md)
