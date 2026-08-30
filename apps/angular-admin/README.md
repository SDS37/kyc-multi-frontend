# Angular Admin

Angular **22+** admin / reviewer portal (`apps/angular-admin`). Foundation: KYC-060. Login: KYC-061. Case UI: KYC-062–064.

**How to write this app:** [Frontend code standards](../../docs/frontend-code-standards.md) (Angular section follows official [angular.dev](https://angular.dev/style-guide) docs). Shared colors/spacing: [UX design tokens](../../docs/ux-design-tokens.md) / `@kyc/design-tokens`. Material theme maps to those tokens (`src/material-theme.scss`) — do not add Bootstrap.

## Prerequisites

- Node.js **20.19+** (22 recommended; see `.nvmrc`)
- API running locally at `http://localhost:5295` (see [apps/api/README.md](../api/README.md))
- CORS already allows `http://localhost:4200`

## Run

```bash
cd apps/angular-admin
npm install
npm start
```

App: `http://localhost:4200` (unauthenticated → `/login`)  
GraphQL (dev env): `http://localhost:5295/graphql`

```bash
npm test       # unit tests (Vitest via Angular CLI)
npm run test:ci
npm run build
```

PRs that touch `apps/angular-admin` (or `.github/workflows/angular-ci.yml`) run GitHub Actions `angular-ci` (`npm ci`, build, `test:ci`).

## What KYC-061 delivers

| Piece | Location |
|---|---|
| Angular Material + token-mapped theme | `@angular/material`, `src/material-theme.scss` |
| Login (tenant slug, email, password) | `src/app/auth/login/` → GraphQL `login` |
| Login service + `SKIP_AUTH` | `src/app/auth/login.service.ts` |
| Auth / guest guards | `src/app/auth/auth.guard.ts` |
| Post-login stub | `src/app/cases/case-list/` (full list = KYC-062) |

Still from KYC-060: `TokenStorage`, functional `authInterceptor`, `APP_CONFIG` / environments.

## Intended responsibilities (W4)

- Shell chrome for the reviewer / Tenant Admin experience (KYC-064)
- Login (tenant slug + email + password) against GraphQL `login` (**KYC-061** — done)
- Case list: title, **customer email**, status, updated date, status filter (KYC-062)
- Case review: form data, documents **with download**, start / approve / reject (KYC-063)

Tenant user and role management is **not** in the API and **not** in KYC-060–064. Do not invent it in this app.

React and Vue stay separate apps for MVP (ADR-005). Auth and cases use the shared GraphQL API; document download is REST `GET /api/cases/{caseId}/documents/{documentId}` with the same JWT.
