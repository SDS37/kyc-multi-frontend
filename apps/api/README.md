# API

.NET 10 host with EF Core, talking to local PostgreSQL from Docker Compose.

New to .NET? Read [the frontend-oriented guide](../../docs/guides/dotnet-api-for-frontend-engineers.md) first. This file is the runbook (restore, migrate, run, test).

`Tenant`, `User`, and `Case` are persisted via EF Core. Hot Chocolate serves `/graphql` (IDE in Development only). `/health` is available. GraphQL is **deny by default** (JWT): only `login` and `registerTenant` mutations are anonymous (KYC-021). Customers create drafts with `createDraftCase` (KYC-031); `reviewerOnlyPing` remains a Reviewer gate stub (KYC-022). Temporary REST `POST /api/register-tenant` and `POST /api/login` stay on the same anonymous allow-list. Login returns a short-lived JWT (`sub`, `tenant_id`, `role`, `email`). Tenant-owned entities implement `ITenantScoped` and are filtered by the JWT tenant (fail closed when unauthenticated; login uses `IgnoreQueryFilters`) (KYC-014). `Case` is tenant-scoped (KYC-030); further case lifecycle GraphQL arrives in later stories.

Local Development listens on **HTTP** (`http://localhost:5295`). That is acceptable for documented Compose credentials only — do not use plain HTTP for real secrets.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) matching [global.json](../../global.json) (`dotnet --version` → `10.0.400` or a roll-forward allowed by that file)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) running
- Compose stack up from the **repo root** (see the root README):

```bash
cp infrastructure/.env.example infrastructure/.env
docker compose -f infrastructure/docker-compose.yml up -d
```

Postgres must be healthy on `127.0.0.1:5432`.

## 1. Local config (Postgres + JWT)

Committed `appsettings.json` keeps empty `ConnectionStrings:Postgres` and `Jwt:SigningKey` (shape only). Local values live in gitignored `appsettings.Development.json`.

Copy the example once (Compose defaults + local-only JWT key ≥32 chars, including `Issuer` / `Audience` / `ExpiresMinutes`):

```bash
cp apps/api/Kyc.Api/appsettings.Development.json.example apps/api/Kyc.Api/appsettings.Development.json
```

Do not commit `appsettings.Development.json`.

Alternatives (pick one instead of the file):

```bash
# Environment variables (__ = nested JSON key)
export ConnectionStrings__Postgres="Host=127.0.0.1;Port=5432;Database=kyc_db;Username=kyc;Password=changeme"
export Jwt__SigningKey="local-dev-only-change-me-32chars-min!!"

# User secrets (profile, not the repo)
cd apps/api/Kyc.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=127.0.0.1;Port=5432;Database=kyc_db;Username=kyc;Password=changeme"
dotnet user-secrets set "Jwt:SigningKey" "local-dev-only-change-me-32chars-min!!"
```

If `Postgres` or `Jwt:SigningKey` is missing/empty (or the key is shorter than 32 characters), the host throws at startup.

## 2. Restore

From the repo root:

```bash
dotnet tool restore
dotnet restore apps/api/Kyc.Api.sln
```

`dotnet tool restore` installs `dotnet-ef` from `.config/dotnet-tools.json`. The solution includes `Kyc.Api` and `Kyc.Api.Tests`.

## 3. Apply migrations

With Compose Postgres running and local config set:

```bash
cd apps/api/Kyc.Api
dotnet ef database update
```

Schema history: `InitialCreate` → `AddTenant` → `AddUser` (unique `(TenantId, Email)`) → `AddCase` (`cases` table, `FormData` as JSON/`jsonb`). KYC-014 added filters only (no new migration).

To add another schema change:

```bash
dotnet ef migrations add NameOfChange --output-dir Data/Migrations
dotnet ef database update
```

## 4. Run

```bash
cd apps/api/Kyc.Api
dotnet run
```

- HTTP: `http://localhost:5295`
- Health: `GET http://localhost:5295/health`
- GraphQL: `http://localhost:5295/graphql` (IDE / Banana Cake Pop in Development only)
- OpenAPI (Development only): `http://localhost:5295/openapi/v1.json`
- Register tenant (anonymous): GraphQL `registerTenant` or temporary `POST /api/register-tenant`
- Login (anonymous; returns JWT): GraphQL `login` or temporary `POST /api/login`
- Authenticated GraphQL fields (e.g. `apiStatus`): `Authorization: Bearer <token>`

Register example:

```json
{
  "tenantName": "Acme Compliance",
  "tenantSlug": "acme",
  "adminEmail": "admin@acme.example",
  "adminPassword": "ChangeMe1"
}
```

Login example:

```json
{
  "tenantSlug": "acme",
  "email": "admin@acme.example",
  "password": "ChangeMe1"
}
```

Successful login returns `{ "accessToken", "tokenType": "Bearer", "expiresInSeconds" }`. Invalid credentials or an inactive tenant return **401** with a generic error. See `Kyc.Api.http`.

GraphQL smoke (anonymous mutations + authenticated query — or use the IDE at `/graphql`):

```graphql
mutation {
  registerTenant(input: {
    tenantName: "Acme Compliance"
    tenantSlug: "acme"
    adminEmail: "admin@acme.example"
    adminPassword: "ChangeMe1"
  }) { tenantSlug }
}

mutation {
  login(input: {
    tenantSlug: "acme"
    email: "admin@acme.example"
    password: "ChangeMe1"
  }) { accessToken tokenType expiresInSeconds }
}

# Send Authorization: Bearer <accessToken>
query { apiStatus }
```

Unauthenticated `apiStatus` returns GraphQL error `AUTH_NOT_AUTHENTICATED`.

Stop the host with Ctrl+C.

## GraphQL operations

Endpoint: `POST /graphql` (IDE in Development). Auth is **deny by default**; send `Authorization: Bearer <accessToken>` unless noted. Copy-paste bodies live in [`Kyc.Api/Kyc.Api.http`](Kyc.Api/Kyc.Api.http). Keep this table as an index when adding fields — prefer the IDE / schema for full types.

### Queries

| Field | Auth | Purpose |
|---|---|---|
| `apiStatus` | Authenticated (any role) | Liveness; returns `"ok"` |

### Mutations

| Field | Auth | Purpose |
|---|---|---|
| `registerTenant` | Anonymous | Create tenant + first TenantAdmin |
| `login` | Anonymous | Issue JWT (`sub`, `tenant_id`, `role`, `email`) |
| `createDraftCase` | Customer | Create draft case; `TenantId` / `CustomerUserId` from JWT only; title required; empty `formData` → `"{}"`; status `DRAFT` |
| `reviewerOnlyPing` | Reviewer | Stub gate until review mutations (returns `"reviewer-ok"`) |

Common GraphQL error codes: `AUTH_NOT_AUTHENTICATED`, `AUTH_NOT_AUTHORIZED`, `VALIDATION`, `AUTH_FAILED` (login or stale/incomplete JWT context on `createDraftCase`). Temporary REST `POST /api/register-tenant` and `POST /api/login` mirror the anonymous mutations.

## 5. Build and test

From the repo root:

```bash
dotnet build apps/api/Kyc.Api.sln
dotnet test apps/api/Kyc.Api.sln
```

PRs that touch `apps/api` (or `global.json` / the workflow file) run the same build/test via GitHub Actions (KYC-102).

## Done checks

| Story | Proof |
|---|---|
| KYC-004 | `dotnet build` / `dotnet run`; OpenAPI at `http://localhost:5295/openapi/v1.json`; ConnectionStrings/Jwt secrets not committed |
| KYC-010 | `tenants` table with unique `Slug`; entity has Id, Name, Slug, IsActive, CreatedAt |
| KYC-011 | `users` with Role TenantAdmin/Reviewer/Customer; FK to one tenant; unique `(TenantId, Email)` |
| KYC-012 | `POST /api/register-tenant` creates Tenant + TenantAdmin in one transaction; password hashed (8–128 chars); validation errors return 400; no JWT required |
| KYC-013 | `POST /api/login` with tenant slug + email + password; JWT claims `sub`, `tenant_id`, `role`, `email`; generic 401 on bad credentials; inactive tenant cannot log in |
| KYC-014 | `ICurrentTenant` from JWT `tenant_id`; EF global filter on `ITenantScoped` (fail closed without tenant); `dotnet test` proves tenant A cannot read tenant B users |
| KYC-020 | `/graphql` (Hot Chocolate); GraphQL IDE in Development only; `GET /health` |
| KYC-021 | Deny-by-default GraphQL JWT auth; anonymous `login` / `registerTenant` only; invalid token rejected; REST on the same allow-list |
| KYC-022 | `[Authorize(Roles = ...)]` on Reviewer/Customer mutations; wrong role → GraphQL `AUTH_NOT_AUTHORIZED` (not HTTP 500) |
| KYC-030 | `Case` with required fields + status enum; `ITenantScoped`; migration `AddCase`; isolation test tenant A cannot read tenant B cases |
| KYC-031 | Customer `createDraftCase`; status `Draft`; title required; empty `FormData` → `{}`; `TenantId`/`CustomerUserId` from JWT only |
| KYC-102 | GitHub Actions `api-ci` builds and tests `apps/api/Kyc.Api.sln`; SDK pinned in `global.json` |

Out of scope here: auth rate limits (KYC-093). Local HTTP is for Development only.
