# Study: repository root

Study tour of this folder. Distinct from the official README.

**Aligned with:** `main` after Week 2 (KYC-037). Case GraphQL lifecycle is live; the three UIs are still placeholders.

Tracked in git so they render on GitHub. They are a tour, not a contract — ADRs and official READMEs win if anything disagrees. Update these files when the code or architecture moves; they can be deleted from the repo later.

This file is the **map of the monorepo**. Open a folder’s `README.STUDY.md` when you want to speak about that layer in a design conversation. Commands, ports, and story checklists stay in the committed READMEs.

## Purpose

One repo holds three future frontends, one .NET GraphQL API, local Docker dependencies, and the written architecture. ADR-001 chose a monorepo so tenant rules, GraphQL contract, and docs stay in one place.

## Why these folders exist

| Folder / file | Why it is here |
|---|---|
| `apps/` | Deployable products. One folder per app so Angular / React / Vue / .NET never share a bundler. |
| `docs/` | Decisions and target architecture. Source of truth for “what we meant.” |
| `infrastructure/` | Postgres / Redis / MinIO via Compose. Not the API container (API still runs on the host). |
| `.github/workflows/` | `api-ci` — the only automated proof that the API still builds and isolates tenants. |
| `.config/` | Local .NET tools (`dotnet-ef`). Analogous to a repo-level `npx` binary pin. |
| `global.json` | Pins the .NET SDK, like an `.nvmrc` / Volta pin. |
| `.editorconfig` | Shared formatting. Not architecture. |

## Angular analog

This is an Nx-style workspace **without Nx**: apps are siblings, shared contract is GraphQL + JWT (not a shared UI library). `docs/` is the architecture board you would otherwise keep in Confluence. `infrastructure/` is the docker-compose you would run instead of installing Postgres on the laptop.

Java analog: a multi-module Maven reactor where only `apps/api` is implemented; the other modules are empty `pom.xml` stubs.

## How a request touches the repo today

```mermaid
sequenceDiagram
    participant You as You (host)
    participant API as Kyc.Api (dotnet run)
    participant PG as Postgres (Compose)

    You->>API: POST /graphql + JWT
    API->>PG: EF Core (tenant filter on)
    PG-->>API: rows for JWT tenant only
    API-->>You: typed GraphQL payload
```

Redis and MinIO are **up but unused** until Week 3 (documents) and later cache/token work. Saying “we use Redis” in a review would be inaccurate today.

## Today vs target

| Target ([architecture](docs/architecture.md), [ADRs](docs/architecture-decision-records.md)) | Today on `main` |
|---|---|
| Three UIs + GraphQL API | API is real; UIs are README placeholders |
| Modular monolith (Identity, Cases, Documents, Audit) | **Layer folders** inside one .NET project |
| CQRS + MediatR | Application **services** called from GraphQL |
| GraphQL as the only public API | Cases are GraphQL; login/register still have temporary REST twins |
| MinIO for KYC files | Compose image is pinned; API does not store files yet |

**What you can say with confidence:** “Week 2 delivered the case lifecycle on Hot Chocolate, with JWT tenant isolation fail-closed in EF. The modular-monolith *shape* is still layers in one host; Documents/Audit are not modules yet.”

## Suggested reading order

1. This file (you are here).
2. [docs/README.STUDY.md](docs/README.STUDY.md) — how committed docs relate; do not duplicate them.
3. [infrastructure/README.STUDY.md](infrastructure/README.STUDY.md) — why Postgres is on `127.0.0.1`.
4. [apps/api/README.STUDY.md](apps/api/README.STUDY.md) — solution vs project vs tests.
5. Follow **one mutation** through [Kyc.Api](apps/api/Kyc.Api/README.STUDY.md) → [GraphQL](apps/api/Kyc.Api/GraphQL/README.STUDY.md) → [Application](apps/api/Kyc.Api/Application/README.STUDY.md) → [Domain](apps/api/Kyc.Api/Domain/README.STUDY.md) → [Data](apps/api/Kyc.Api/Data/README.STUDY.md).
6. [Tests](apps/api/Kyc.Api.Tests/README.STUDY.md) — especially tenant isolation. This is the sentence isolation conversations hang on.
7. [api-ci](.github/workflows/README.STUDY.md) — what CI actually proves.
8. Frontend placeholders last: [angular-admin](apps/angular-admin/README.STUDY.md), [react-customer](apps/react-customer/README.STUDY.md), [vue-reports](apps/vue-reports/README.STUDY.md).

## What to skip

- `bin/`, `obj/`, `node_modules/` if they appear later — build output.
- `.git/` — history, not design.
- Generated EF `*.Designer.cs` until you have read [Migrations/README.STUDY.md](apps/api/Kyc.Api/Data/Migrations/README.STUDY.md).

## Links

- Run the repo: [README.md](README.md)
- API runbook: [apps/api/README.md](apps/api/README.md)
- Frontend-oriented .NET map: [docs/guides/dotnet-api-for-frontend-engineers.md](docs/guides/dotnet-api-for-frontend-engineers.md) — still useful; its “still ahead” table lags Week 2 (cases GraphQL is on `main`).
- [Roadmap](docs/roadmap.md) (W2 = KYC-020–037)
- [ADR-001 monorepo](docs/architecture-decision-records.md)
- [ADR-007 tenant from JWT](docs/architecture-decision-records.md)
- [.NET SDK / global.json](https://learn.microsoft.com/dotnet/core/tools/global-json)
- [Conventional Commits](https://www.conventionalcommits.org/) used by [docs/commits.md](docs/commits.md)
