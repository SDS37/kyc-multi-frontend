# Study: `apps/vue-reports`

**Aligned with:** `feat/kyc-081-vue-case-overview` / KYC-081 (counts + latest 10 on the KYC-080 shell).

## Purpose

Read-only reports portal for **Reviewer / TenantAdmin** (ADR-004). Status counts and the latest ten cases reuse GraphQL `cases` (KYC-036) with aliases — no new backend field.

## Map

| Concern | Where |
|---|---|
| Smart login screen | `src/auth/login-page/LoginPage.vue` |
| Authenticated chrome | `src/layout/ReportsShell.vue` |
| Smart reports home | `src/reports/ReportsHome.vue` |
| Presentational counts / table | `ReportsStatusCounts.vue`, `ReportsLatestTable.vue` |
| GraphQL | `src/shared/http.ts` + `src/reports/reports-api.ts` |
| Pure parse | `src/reports/reports.mappers.ts` |
| Router | `src/app-router.ts` — `beforeEach` **returns** a location (no `next()`) |

## Angular / React differences

Admin list is a review queue with mutations and title links. Customer owns draft/upload. Vue is **read-only**: counts + a 10-row table with no links and no mutations. Same JWT; role filter is stricter (Customer blocked).

Latest 10 uses API order (newest `Id` first), not `updatedAt`.

## Links

- [README.md](README.md)
- [Issue #37](https://github.com/SDS37/kyc-multi-frontend/issues/37) (KYC-081)
- [Vue Style Guide](https://vuejs.org/style-guide/)
- [roadmap time-control](../../docs/roadmap.md)
