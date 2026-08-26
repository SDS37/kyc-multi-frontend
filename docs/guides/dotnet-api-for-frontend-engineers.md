# .NET API for frontend engineers

This is a conceptual map of the KYC API scaffold ([KYC-004](https://github.com/SDS37/kyc-multi-frontend/issues/45)). It is for people who are strong on Angular/React/Vue and new to backend .NET.

**Commands to run the app** live in [`apps/api/README.md`](../../apps/api/README.md). Do not treat this file as a runbook; versions and flags change.

KYC-004 is: an empty but real host that can talk to Postgres. It is the .NET equivalent of `ng new` / `npm create vite` **plus** wiring Prisma or TypeORM to a database. It does **not** include Tenant, GraphQL, or login (those are KYC-010, KYC-020, KYC-013).

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

**Cursor is enough as an editor.** Visual Studio (the full IDE) and Rider are optional, like buying WebStorm when Cursor already edits the code. Install the **C#** extension in Cursor for IntelliSense.

**You need the .NET SDK, not only the Runtime.** Runtime = run compiled apps. SDK = `dotnet new`, `dotnet build`, `dotnet ef`. This repo targets **.NET 10** (`net10.0`). Check with `dotnet --version`.

**You need Docker Desktop**, not a native Postgres install. Compose starts PostgreSQL (and Redis/MinIO). For this API, only **Postgres** on `127.0.0.1:5432` matters. Start Docker Desktop until it is running, then `docker version` and `docker compose version` should work.

**You do not need** pgAdmin, Redis GUIs, or to put the API itself in Docker for KYC-004. The API runs on the Mac and connects to Postgres on localhost. Inside Compose the hostname is `postgres`; **from your machine it is `127.0.0.1`**. Mixing those up is the usual connection error.

## What the scaffold actually is

- **`Program.cs`** — composition root (`main.ts` + app config). Registers OpenAPI in Development and EF Core.
- **`AppDbContext`** — empty on purpose (no `DbSet`s). KYC-010 adds Tenant.
- **`UseNpgsql`** — Postgres provider (the `pg` driver equivalent).
- **HTTPS redirect** — turned off for local HTTP. The template warning “Failed to determine the https port” is normal if redirect is enabled without an HTTPS URL.

The `dotnet new webapi` weather-forecast sample is not part of KYC; it was removed.

## Secrets

`appsettings.json` keeps an empty `ConnectionStrings:Postgres` key so the shape is committed.

Local password `changeme` is the **documented Compose default**, not a production secret. Still do **not** commit `appsettings.Development.json` (it is gitignored). Commit only `appsettings.Development.json.example`.

Three ways to supply the string (pick one; see the API README for copy-paste):

1. Copy the example → `appsettings.Development.json` (simplest).
2. Environment variable `ConnectionStrings__Postgres`. The `__` is nested JSON (`ConnectionStrings:Postgres`), same idea as Vite env prefixes.
3. `dotnet user-secrets` — stored in your user profile, not the repo.

Do not put passwords in `launchSettings.json`; that file is committed.

If the string is missing, the host throws at startup. That is intentional.

## Migrations

EF migrations are versioned schema, like Prisma’s `migrations/` folder.

`InitialCreate` exists with empty `Up`/`Down` because there are no entities yet. Applying it only creates `__EFMigrationsHistory`. That proves the pipeline. Tenant will be a **new** migration in KYC-010.

`dotnet-ef` is a **local tool** in `.config/dotnet-tools.json` (`dotnet tool restore` from the repo root), similar to a pinned CLI in the repo rather than a global npm -g install.

`database update` needs Compose Postgres **healthy** and a connection string. Starting the API before Postgres is ready fails the same way a UI fails when the API is still booting.

## What “done” means for the scaffold

The host **builds**, **listens** (HTTP `http://localhost:5295`, OpenAPI in Development), **migrates** against Compose Postgres, and **does not commit** Development connection files. There is still no `/graphql` and no Tenant table.

For the exact commands, use [`apps/api/README.md`](../../apps/api/README.md).
