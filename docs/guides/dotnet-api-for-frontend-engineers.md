# .NET API for frontend engineers

Conceptual map of the KYC .NET API for people who are strong on Angular/React/Vue and new to backend .NET.

**Commands to run the app** live in [`apps/api/README.md`](../../apps/api/README.md). This file is not a runbook; versions and flags change.

## Where we are vs the target

| Already on `main` | Still ahead (roadmap) |
|---|---|
| .NET host + EF Core + Postgres | Hot Chocolate GraphQL (KYC-020) |
| `Tenant` and `User` (+ roles) | JWT login (KYC-013), tenant isolation (KYC-014) |
| Temporary `POST /api/register-tenant` | Cases, documents, audit, three UI apps |

The **target** remains one GraphQL API, CQRS modular monolith, JWT tenant context, and three frontends — see [architecture](../architecture.md) and [ADRs](../architecture-decision-records.md).

KYC-004 was the empty-host step (`ng new` / `npm create vite` **plus** wiring an ORM). Identity stories built on that scaffold.

## Frontend → .NET cheat sheet

| You already know | In this API |
|---|---|
| `package.json` | `.csproj` |
| `npm install` / lockfile | `dotnet restore` (NuGet) |
| `npm start` | `dotnet run` |
| `.env` (gitignored) + `.env.example` | `appsettings.json` + gitignored `appsettings.Development.json` + `.example` |
| Prisma Client / TypeORM `DataSource` | EF Core `DbContext` |
| `prisma migrate` | `dotnet ef migrations` / `database update` |
| Vite/Angular port in config | `Properties/launchSettings.json` |

## Software: what you need and what you can skip

**Cursor is enough as an editor.** Visual Studio and Rider are optional. Install the **C#** extension in Cursor for IntelliSense.

**You need the .NET SDK, not only the Runtime.** Runtime = run compiled apps. SDK = `dotnet new`, `dotnet build`, `dotnet ef`. This repo targets **.NET 10** (`net10.0`). Check with `dotnet --version`.

**You need Docker Desktop**, not a native Postgres install. Compose starts PostgreSQL (and Redis/MinIO). For the API, only **Postgres** on `127.0.0.1:5432` is required today. Start Docker Desktop until it is running, then `docker version` and `docker compose version` should work.

**You do not need** pgAdmin or Redis GUIs for the first slice. The API runs on the host and connects to Postgres on localhost. Inside Compose the hostname is `postgres`; **from your machine it is `127.0.0.1`**. Mixing those up is the usual connection error.

## What the API project is

- **`Program.cs`** — composition root. Registers EF Core, OpenAPI (Development), password hasher, and the temporary register endpoint.
- **`AppDbContext`** — EF session with `Tenants` and `Users` (and more as stories land).
- **`UseNpgsql`** — Postgres provider (the `pg` driver equivalent).
- **Local HTTP** — Development uses `http://localhost:5295`. Fine for local Compose credentials; do not treat that as a production pattern for passwords.

## Secrets

`appsettings.json` keeps an empty `ConnectionStrings:Postgres` key so the shape is committed.

Local password `changeme` is the **documented Compose default**, not a production secret. Still do **not** commit `appsettings.Development.json` (it is gitignored). Commit only `appsettings.Development.json.example`.

Three ways to supply the string (pick one; see the API README):

1. Copy the example → `appsettings.Development.json` (simplest).
2. Environment variable `ConnectionStrings__Postgres` (`__` = nested JSON).
3. `dotnet user-secrets` — stored in your user profile, not the repo.

Do not put passwords in `launchSettings.json`; that file is committed.

## Migrations

EF migrations are versioned schema, like Prisma’s `migrations/` folder.

History includes `InitialCreate` (empty pipeline proof), then `AddTenant` and `AddUser`. Apply with `dotnet ef database update` after Compose Postgres is healthy.

`dotnet-ef` is a **local tool** in `.config/dotnet-tools.json` (`dotnet tool restore` from the repo root).

## Next identity steps

Register is temporary REST. Login + JWT (KYC-013) and GraphQL (KYC-020) replace the long-term public surface. For exact commands, use [`apps/api/README.md`](../../apps/api/README.md).
