# API

.NET 10 host with EF Core, talking to local PostgreSQL from Docker Compose.

New to .NET? Read [the frontend-oriented guide](../../docs/guides/dotnet-api-for-frontend-engineers.md) first. This file is the runbook (restore, migrate, run).

GraphQL, JWT, and domain entities (Tenant, User, cases) are **not** in this project yet. Those are later stories (KYC-010, KYC-013, KYC-020).

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

`InitialCreate` is already in `Data/Migrations`. With an empty `DbContext` it only creates `__EFMigrationsHistory`. New tables (e.g. Tenant) get a new migration later:

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

There is no `/graphql` endpoint yet.

```bash
dotnet build
```

succeeds if restore worked. Stop the host with Ctrl+C.

## How you know KYC-004 is done

| Criterion | Proof |
|---|---|
| Project builds and starts | `dotnet build` and `dotnet run` in `apps/api/Kyc.Api`; OpenAPI at `http://localhost:5295/openapi/v1.json` returns 200 |
| EF Core talks to Compose Postgres | `dotnet ef database update` succeeds (`InitialCreate` / `__EFMigrationsHistory`) |
| No secrets committed | `appsettings.Development.json` is gitignored; only `.example` is tracked |
| README | this file covers restore, migrate, and run |

Out of scope (do **not** treat as missing): Tenant entity, GraphQL, JWT. Those are KYC-010 / KYC-020 / KYC-013.
