# Angular Admin

Angular **22+** admin / reviewer portal (`apps/angular-admin`). Foundation: KYC-060. Login and case UI: KYC-061–064.

**How to write this app:** [Frontend code standards](../../docs/frontend-code-standards.md) (Angular section follows official [angular.dev](https://angular.dev/style-guide) docs).

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

App: `http://localhost:4200`  
GraphQL (dev env): `http://localhost:5295/graphql`

```bash
npm test    # unit tests (Vitest via Angular CLI)
npm run build
```

## What KYC-060 delivers

| Piece | Location |
|---|---|
| Standalone app + router | `src/main.ts`, `src/app/app.routes.ts` |
| Shell home route | `src/app/shell/` |
| GraphQL / API URLs | `src/environments/` → `APP_CONFIG` |
| Token storage | `src/app/auth/token-storage.ts` (`sessionStorage`) |
| Functional auth interceptor | `src/app/auth/auth.interceptor.ts` via `provideHttpClient(withInterceptors([…]))` |
| Skip auth for anonymous calls | `SKIP_AUTH` `HttpContextToken` (for KYC-061 login) |

## Intended responsibilities (W4)

- Shell chrome and routing for the reviewer / Tenant Admin experience (KYC-064)
- Login (tenant slug + email + password) against GraphQL `login` (KYC-061)
- Case list: title, **customer email**, status, updated date, status filter (KYC-062)
- Case review: form data, documents **with download**, start / approve / reject (KYC-063)

Tenant user and role management is **not** in the API and **not** in KYC-060–064. Do not invent it in this app.

React and Vue stay separate apps for MVP (ADR-005). Auth and cases use the shared GraphQL API; document download is REST `GET /api/cases/{caseId}/documents/{documentId}` with the same JWT.
