# Vue Reports

Vue **3.5+** reports portal (`apps/vue-reports`). KYC-080 login/shell + **KYC-081** read-only status counts and latest-10 cases.

**How to write this app:** [Frontend code standards](../../docs/frontend-code-standards.md) (shared rules + [Vue section](../../docs/frontend-code-standards.md#vue-appsvue-reports)). Shared `--kyc-*` tokens match Angular admin and React customer ([ux-design-tokens.md](../../docs/ux-design-tokens.md)).

## Prerequisites

- Node.js **20.19+** (22 recommended; see `.nvmrc`)
- API at `http://localhost:5295`
- CORS allows `http://localhost:5174`

## Run

```bash
cd apps/vue-reports
npm install
npm start
```

App: `http://localhost:5174` → `/login` (guests) or `/reports` (Reviewer / TenantAdmin)

```bash
npm test
npm run test:ci
npm run build
```

PRs that touch `apps/vue-reports` (or `.github/workflows/vue-ci.yml`) run GitHub Actions `vue-ci` (`npm ci`, lint, `vue-tsc` + Vite build, `test:ci`).

## Demo login

`registerTenant` creates **TenantAdmin** — that role can sign in here. Reviewer users also work. **Customer sessions are rejected** (use the React portal).

```json
{ "tenantSlug": "acme", "adminEmail": "admin@acme.example", "adminPassword": "ChangeMe1234", "tenantName": "Acme" }
```

## Security notes

- GraphQL `login` is anonymous (`skipAuth`). All other calls attach JWT `Authorization` — never send `tenantId` / role in bodies (ADR-007)
- Session lives in `sessionStorage` (tab close clears it)
- HTTP 401 and GraphQL `AUTH_NOT_AUTHENTICATED` clear the session
- `returnUrl` must be an in-app path (`/` but not `//`) — open redirects are blocked
- Route guards are UX only; the API still enforces JWT
- `/reports` is **read-only** (counts + table). No case mutations
- Templates never use `v-html`

## Component tree (KYC-081)

Living render tree for this app (update when routes change). Smart vs presentational **rules:** [frontend-code-standards](../../docs/frontend-code-standards.md#component-tree-all-frontends). System composition: [architecture.md §3](../../docs/architecture.md#3-frontend-composition).

```mermaid
flowchart TB
  Main["main.ts"] --> App["App"]
  App --> Login["/login LoginPage"]
  App --> Shell["ReportsShell"]
  Shell --> Home["/reports ReportsHome"]
  Home --> Loading["loading status"]
  Home --> Counts["ReportsStatusCounts"]
  Home --> Table["ReportsLatestTable"]
  Home --> LoadErr["ReportsLoadError"]
```

| Piece | Location |
|---|---|
| Login | `src/auth/login-page/LoginPage.vue` |
| Authenticated shell | `src/layout/ReportsShell.vue` |
| Reports home (smart) | `src/reports/ReportsHome.vue` |
| Status counts | `src/reports/ReportsStatusCounts.vue` |
| Latest-10 table | `src/reports/ReportsLatestTable.vue` |
| Load error | `src/reports/ReportsLoadError.vue` |
| Reports API / mappers | `src/reports/reports-api.ts`, `reports.mappers.ts` |
| Auth API / mappers | `src/auth/login-api.ts`, `auth.mappers.ts` |
| GraphQL helper | `src/shared/http.ts` |
