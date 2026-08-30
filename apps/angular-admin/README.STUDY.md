# Study: `apps/angular-admin`

Study tour of this folder. Distinct from the official README. Official runbook: [README.md](README.md).

**Aligned with:** `feat/kyc-060-angular-foundation` / KYC-060 (+ W4 preflight CORS / `customerEmail`).

## Purpose

Tenant Admin / Reviewer product (ADR-004). KYC-060 is the **scaffold**: standalone Angular 22+, routing, env GraphQL URL, token storage, functional auth interceptor. Login and cases are later stories.

## Why these folders exist

| Path | Role |
|---|---|
| `src/app/shell/` | Foundation home route (lazy-loaded) |
| `src/app/auth/` | `TokenStorage` + `authInterceptor` + `SKIP_AUTH` |
| `src/app/config/` | `APP_CONFIG` injection token |
| `src/environments/` | Dev/prod API + GraphQL URLs (file replacement) |

Feature folders, not `components/` / `services/` type buckets ([frontend code standards](../../docs/frontend-code-standards.md) / [angular.dev style guide](https://angular.dev/style-guide)).

## What it consumes

Same API as the other two UIs. No private BFF.

| Need | GraphQL / auth |
|---|---|
| Login | `login` mutation → JWT (`sub`, `tenant_id`, `role`, `email`) — **KYC-061** |
| Review queue | `cases` with `status` filter; `customerEmail`; Reviewer sees **all tenant** cases |
| Detail | `case(id)` — FormData, comments, `customerEmail`, document metadata |
| Download | REST `GET /api/cases/{caseId}/documents/{documentId}` (same JWT) |
| Start / decide | `startCaseReview`, `approveCase` / `rejectCase` |

Auth header: `Authorization: Bearer <accessToken>` via interceptor. Tenant id is **not** a query param (ADR-007).

Role: this app is for `Reviewer` and `TenantAdmin`. Guards are UX, not security.

```mermaid
flowchart LR
    Admin["Angular Admin"]
    GQL["POST /graphql"]
    Admin -->|"JWT role Reviewer or TenantAdmin"| GQL
```

## Module Federation

ADR-005: MVP is **three independent apps**. Do not design this app as a federation host in W4.

Local URL: `http://localhost:4200`. CORS: KYC-091.

## Today vs target

| Target | Today (KYC-060) |
|---|---|
| Runnable Angular 22+ app | Yes |
| Env GraphQL + auth interceptor | Yes |
| Login / case list / review | Later (061–063) |
| Angular shell composing remotes | Not required for MVP |

## What to skip

- Inventing user-management screens
- Calling MinIO / Postgres from the browser
- NgModules for features
- Apollo until a story chooses a GraphQL client (HttpClient POST to `/graphql` is enough to start)

## Links

- [README.md](README.md)
- [Frontend code standards](../../docs/frontend-code-standards.md)
- [ADR-004 / ADR-005 / ADR-007](../../docs/architecture-decision-records.md)
- [API GraphQL STUDY](../api/Kyc.Api/GraphQL/README.STUDY.md)
- [angular.dev](https://angular.dev/)
