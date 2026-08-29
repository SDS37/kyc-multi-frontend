# API

.NET 10 host with EF Core, talking to local PostgreSQL from Docker Compose.

New to .NET? Read [the frontend-oriented guide](../../docs/guides/dotnet-api-for-frontend-engineers.md) first. This file is the runbook (restore, migrate, run, test).

`Tenant`, `User`, `Case`, and `Document` are persisted via EF Core. Hot Chocolate serves `/graphql` (IDE in Development only). `/health` is available. GraphQL is **deny by default** (JWT): only `login` and `registerTenant` mutations are anonymous (KYC-021). Customers create/update/submit with `createDraftCase` / `updateDraftCase` / `submitCase` (KYC-031–033); Reviewer/TenantAdmin start review with `startCaseReview` (KYC-034) and finish with `approveCase` / `rejectCase` (KYC-035). Authenticated users list with `cases` (KYC-036) and open detail with `case` (KYC-037). Customers upload files with REST `POST /api/cases/{caseId}/documents` (KYC-040; MinIO via `ObjectStorage`; metadata on case detail). Temporary REST `POST /api/register-tenant` and `POST /api/login` stay on the same anonymous allow-list. Login returns a short-lived JWT (`sub`, `tenant_id`, `role`, `email`). Tenant-owned entities implement `ITenantScoped` and are filtered by the JWT tenant (fail closed when unauthenticated; login uses `IgnoreQueryFilters`) (KYC-014).

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

Copy the example once (Compose defaults + local-only JWT key ≥32 chars, including `Issuer` / `Audience` / `ExpiresMinutes`, plus `ObjectStorage` MinIO settings for KYC-040):

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
- Health (liveness): `GET http://localhost:5295/health`
- Ready (Postgres): `GET http://localhost:5295/ready`
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

## Health vs readiness (KYC-103)

| Endpoint | Role | Auth | When it fails |
|---|---|---|---|
| `GET /health` | Liveness (process is up) | Anonymous | Almost never — tagged `live` only; does **not** open Postgres |
| `GET /ready` | Readiness (can serve traffic) | Anonymous | **503** when Postgres is unreachable |

Orchestrators should restart on `/health` failure and stop sending traffic on `/ready` failure. Do not point liveness at `/ready`.

Timeouts and retries (configured in `Resilience` in `appsettings.json`):

| Setting | Default | Purpose |
|---|---|---|
| `NpgsqlCommandTimeoutSeconds` | 30 | Per-command Npgsql timeout (EF Core) |
| `EfMaxRetryCount` / `EfMaxRetryDelaySeconds` | 5 / 10 | `EnableRetryOnFailure` for transient Postgres errors |
| `RequestTimeoutSeconds` | 60 | ASP.NET request-timeout middleware (cooperative via `RequestAborted`) |

The request timeout is longer than a single command timeout so a brief retry can still succeed. `/health` and `/ready` disable the request-timeout policy; the ready probe uses a 2s Npgsql timeout of its own.

## Observability (KYC-104)

Stdout is **JSON** (`Logging:Console:FormatterName` = `json` in `appsettings.json`) with scopes enabled so every line can carry `RequestId`.

| What | Where |
|---|---|
| Correlation id | Request header / response `X-Request-Id` (safe token echoed; otherwise Kestrel `TraceIdentifier`). Also in the log scope as `RequestId` (and `TraceId` when an `Activity` exists). |
| HTTP/GraphQL calls | One `HTTP {method} {path} {status} {ms}` Information line per request. **No** bodies, query strings, or headers (login passwords, JWTs, and FormData stay out). |
| Auth failures | `Login rejected` (REST/GraphQL login); `GraphQL auth failure {code}`; `JWT authentication failed {type}`. Codes/types only — no email, password, or token. |
| Readiness failures | `Readiness check failed: Postgres is unreachable ({ExceptionType})`. No connection string. |
| Liveness probes | `GET /health` is **not** logged at Information (avoids probe flood). |

**Read local logs:** run the API in a terminal (`dotnet run` from `apps/api/Kyc.Api`). Pretty-print with `jq` if you have it (`dotnet run | jq .`). Filter one request: look for the `X-Request-Id` response header, then grep that value in stdout (`RequestId` scope).

**Minimal metrics / signals (no APM vendor):** there is no scrape endpoint in MVP. Operators can (later) alert on `GET /ready` → 503 and count `HTTP … {status}` log lines. OpenTelemetry exporters can be added later without changing this contract.

## GraphQL operations

Endpoint: `POST /graphql` (IDE, introspection, and SDL `?sdl` in Development — KYC-105). Auth is **deny by default**; send `Authorization: Bearer <accessToken>` unless noted. Copy-paste bodies live in [`Kyc.Api/Kyc.Api.http`](Kyc.Api/Kyc.Api.http). Keep this table as an index when adding fields — prefer the IDE / schema for full types.

### Queries

| Field | Auth | Purpose |
|---|---|---|
| `apiStatus` | Authenticated (any role) | Liveness; returns `"ok"` |
| `cases` | Authenticated (any role) | List visible cases (no `formData`); Customer = own only; Reviewer/TenantAdmin = all tenant; optional `status`; `skip`/`take` (default take 20, max 100); returns `items`, `totalCount`, `skip`, `take` |
| `case` | Authenticated (any role) | Detail by `id`; same visibility as `cases`; returns `case` (incl. `formData`), `comments` (from `reviewComment`), `documents` (metadata only — never file bytes) |

### REST (temporary / dedicated)

| Endpoint | Auth | Purpose |
|---|---|---|
| `POST /api/register-tenant` | Anonymous | Same as GraphQL `registerTenant` |
| `POST /api/login` | Anonymous | Same as GraphQL `login` |
| `POST /api/cases/{caseId}/documents` | Customer | Multipart upload (`file`); Draft/Submitted + owner only; PDF/PNG/JPG; max 10 MB; stores in MinIO; returns metadata |

### Mutations

| Field | Auth | Purpose |
|---|---|---|
| `registerTenant` | Anonymous | Create tenant + first TenantAdmin |
| `login` | Anonymous | Issue JWT (`sub`, `tenant_id`, `role`, `email`) |
| `createDraftCase` | Customer | Create draft case; `TenantId` / `CustomerUserId` from JWT only; title required; empty `formData` → `"{}"`; `formData` max 64 KiB / depth 8; status `DRAFT` |
| `updateDraftCase` | Customer | Update own draft (`title` required; `formData` optional, max 64 KiB / depth 8); missing / not owner → `NOT_FOUND`; owner non-draft → `DOMAIN` |
| `submitCase` | Customer | Submit own draft by `id`; missing / not owner → `NOT_FOUND`; FormData max 64 KiB / depth 8 with `fullName`, `dateOfBirth` (YYYY-MM-DD), `nationality`, `address`; owner non-draft → `DOMAIN`; sets `SUBMITTED` + `submittedAt` |
| `startCaseReview` | Reviewer or TenantAdmin | Move submitted case to `IN_REVIEW`; sets `ReviewedBy` from JWT; same-tenant only |
| `approveCase` | Reviewer or TenantAdmin | `IN_REVIEW` → `APPROVED`; optional `comment`; sets `ReviewedAt` / `ReviewedBy` / `ReviewComment` |
| `rejectCase` | Reviewer or TenantAdmin | `IN_REVIEW` → `REJECTED`; required `comment`; sets `ReviewedAt` / `ReviewedBy` / `ReviewComment` |

Common GraphQL error codes: `AUTH_NOT_AUTHENTICATED`, `AUTH_NOT_AUTHORIZED`, `VALIDATION`, `AUTH_FAILED`, `NOT_FOUND`, `DOMAIN`. Temporary REST `POST /api/register-tenant` and `POST /api/login` mirror the anonymous mutations. Document upload uses dedicated REST (see table above).

## 5. Build and test

From the repo root:

```bash
dotnet build apps/api/Kyc.Api.sln
dotnet test apps/api/Kyc.Api.sln
```

PRs that touch `apps/api` (or `global.json` / the workflow file) run the same build/test via GitHub Actions (KYC-102 / KYC-108). Live Postgres tests run in CI when `KYC_TEST_POSTGRES` is set; locally they skip unless you export that connection string (Compose defaults work).

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
| KYC-032 | Customer `updateDraftCase`; missing/not owner → `NOT_FOUND`; Draft-only; title/FormData; owner other statuses → `DOMAIN` |
| KYC-033 | Customer `submitCase`; missing/not owner → `NOT_FOUND`; Draft→Submitted; FormData requires fullName/dateOfBirth/nationality/address; `SubmittedAt` set |
| KYC-034 | Reviewer/TenantAdmin `startCaseReview`; Submitted→InReview; same tenant; sets `ReviewedBy` |
| KYC-035 | Reviewer/TenantAdmin `approveCase` / `rejectCase`; InReview only; reject requires comment; sets `ReviewedAt` / `ReviewedBy` / `ReviewComment` |
| KYC-036 | Authenticated `cases` query; Customer own-only; Reviewer/TenantAdmin tenant-wide; status filter; skip/take pagination |
| KYC-037 | Authenticated `case(id)` detail; same visibility as list; FormData + comments; document metadata (no bytes) |
| KYC-040 | Customer `POST /api/cases/{id}/documents`; Draft/Submitted; PDF/PNG/JPG ≤10 MB; MinIO + metadata; owner only |
| KYC-102 | GitHub Actions `api-ci` builds and tests `apps/api/Kyc.Api.sln`; SDK pinned in `global.json` |
| KYC-103 | `GET /health` stays a process check; `GET /ready` fails when Postgres is unreachable; EF `EnableRetryOnFailure`; Npgsql command timeout 30s; ASP.NET request timeout 60s |
| KYC-104 | JSON stdout logs with `RequestId`; auth and `/ready` failures logged without secrets; README documents local logs + `/ready`-based signals |
| KYC-105 | Introspection + SDL Development-only; execution depth 10; EF `Database.Command` Warning in `appsettings.json`; MinIO image pinned |
| KYC-106 | Non-owner update/submit → `NOT_FOUND`; FormData 64 KiB / depth 8; atomic submit and start-review status updates |
| KYC-107 | Login dummy password verify on miss paths; `registerTenant` uses EF execution strategy |
| KYC-108 | `api-ci` SHA-pinned actions, `contents: read`, vuln list (warn), thin Postgres migrate + jsonb tests |
| KYC-109 | Login password max 128; `updateDraftCase` DOMAIN before FormData; status docs for 105–108 |

Out of scope here: auth rate limits (KYC-093). CORS/headers are KYC-091. Local HTTP is for Development only.
