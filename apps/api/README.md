# API

.NET 10 host with EF Core, talking to local PostgreSQL from Docker Compose.

New to .NET? Read [the frontend-oriented guide](../../docs/guides/dotnet-api-for-frontend-engineers.md) first. This file is the runbook (restore, migrate, run).

`Tenant` and `User` are persisted via EF Core. Public tenant registration is temporary REST (`POST /api/register-tenant`) until GraphQL (KYC-020). JWT login is KYC-013.

Local Development listens on **HTTP** (`http://localhost:5295`). That is acceptable for documented Compose credentials only — do not use plain HTTP for real secrets.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`dotnet --version` should print 10.x)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) running
- Compose stack up from the **repo root** (see the root README):

```bash
cp infrastructure/.env.example infrastructure/.env
docker compose -f infrastructure/docker-compose.yml up -d
```

Postgres must be healthy on `127.0.0.1:5432`.

## 1. Connection string (do not commit secrets)

`appsettings.json` has an empty `ConnectionStrings:Postgres` key on purpose.

Copy the example (gitignored target). Values match Compose local defaults:

```bash
cp apps/api/Kyc.Api/appsettings.Development.json.example apps/api/Kyc.Api/appsettings.Development.json
```

`appsettings.Development.json` is gitignored. Do not commit it.

Other options (pick one):

```bash
# Environment variable (__ = nested JSON key)
export ConnectionStrings__Postgres="Host=127.0.0.1;Port=5432;Database=kyc_db;Username=kyc;Password=changeme"

# User secrets (stored in your profile, not the repo)
cd apps/api/Kyc.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=127.0.0.1;Port=5432;Database=kyc_db;Username=kyc;Password=changeme"
```

If `Postgres` is missing or empty, the host throws at startup.

## 2. Restore

From the repo root:

```bash
dotnet tool restore
cd apps/api/Kyc.Api
dotnet restore
```

`dotnet tool restore` installs `dotnet-ef` from `.config/dotnet-tools.json`.

## 3. Apply migrations

Still in `apps/api/Kyc.Api`, with Compose Postgres running and a connection string set:

```bash
dotnet ef database update
```

Migrations in `Data/Migrations` include `InitialCreate`, `AddTenant`, and `AddUser` (`users` table; unique `(TenantId, Email)`). After pull:

```bash
dotnet ef database update
```

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
- OpenAPI (Development only): `http://localhost:5295/openapi/v1.json`
- Register tenant (public, no JWT): `POST /api/register-tenant`

Example body:

```json
{
  "tenantName": "Acme Compliance",
  "tenantSlug": "acme",
  "adminEmail": "admin@acme.example",
  "adminPassword": "ChangeMe1"
}
```

See `Kyc.Api.http` for a ready-to-run request. There is no `/graphql` endpoint yet.

```bash
dotnet build
```

succeeds if restore worked. Stop the host with Ctrl+C.

## Done checks

| Story | Proof |
|---|---|
| KYC-004 | `dotnet build` / `dotnet run`; OpenAPI at `http://localhost:5295/openapi/v1.json`; connection string not committed |
| KYC-010 | `tenants` table with unique `Slug`; entity has Id, Name, Slug, IsActive, CreatedAt |
| KYC-011 | `users` with Role TenantAdmin/Reviewer/Customer; FK to one tenant; unique `(TenantId, Email)` |
| KYC-012 | `POST /api/register-tenant` creates Tenant + TenantAdmin in one transaction; password hashed (8–128 chars); validation errors return 400; no JWT required |

Out of scope here: login/JWT, GraphQL (KYC-013 / KYC-020). Local HTTP is for Development only.
