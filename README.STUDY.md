# Study: repository root

Study tour of this folder. Distinct from the official README.

**Aligned with:** KYC-081 Vue reports overview (counts + latest 10). Next: W6 seed + security hardening.

Tracked in git so they render on GitHub. They are a tour, not a contract — ADRs and official READMEs win if anything disagrees. Update these files when the code or architecture moves; they can be deleted from the repo later.

This file is the **map of the monorepo**. Open a folder’s `README.STUDY.md` when you want to speak about that layer in a design conversation. Commands, ports, and story checklists stay in the committed READMEs.

## Purpose

One repo holds three frontends, one .NET GraphQL API, local Docker dependencies, and the written architecture. ADR-001 chose a monorepo so tenant rules, GraphQL contract, and docs stay in one place.

## Why these folders exist

| Folder / file | Why it is here |
|---|---|
| `apps/` | Deployable products. One folder per app so Angular / React / Vue / .NET never share a bundler. |
| `packages/` | Shared **non-UI-framework** libs (e.g. `design-tokens` CSS). Not a second monorepo app. |
| `docs/` | Decisions, **today** architecture, after-MVP wishlist. Source of truth for “what we meant.” |
| `infrastructure/` | Postgres / Redis / MinIO via Compose. Not the API container (API still runs on the host). |
| `.github/workflows/` | `api-ci` + `angular-ci` + `react-ci` + `vue-ci` — automated proof API and UI apps still build/test. |
| `.config/` | Local .NET tools (`dotnet-ef`). Analogous to a repo-level `npx` binary pin. |
| `global.json` | Pins the .NET SDK, like an `.nvmrc` / Volta pin. |
| `.editorconfig` | Shared formatting. Not architecture. |

## Angular analog

This is an Nx-style workspace **without Nx**: apps are siblings, shared contract is GraphQL + JWT (not a shared UI library). `docs/` is the architecture board you would otherwise keep in Confluence. `infrastructure/` is the docker-compose you would run instead of installing Postgres on the laptop.

Java analog: a multi-module Maven reactor where `apps/api`, `apps/angular-admin`, `apps/react-customer`, and `apps/vue-reports` are real modules.

## How a request touches the repo today

```mermaid
sequenceDiagram
    participant You as You (host)
    participant UI as Angular, React, or Vue (browser)
    participant API as Kyc.Api (dotnet run)
    participant PG as Postgres (Compose)
    participant MN as MinIO (Compose)

    You->>UI: Login / cases / upload
    UI->>API: POST /graphql + JWT
    API->>PG: EF Core (tenant filter on)
    PG-->>API: rows for JWT tenant only
    API-->>UI: typed GraphQL payload
    UI->>API: POST /api/cases/{id}/documents multipart
    API->>MN: Put object (StorageKey)
    API->>PG: Insert Document metadata
    API-->>UI: metadata JSON (no StorageKey)
```

Redis is **up but unused**. MinIO holds document **bytes**; Postgres holds document **metadata** (KYC-040–042).

## Today vs target

| Target ([architecture](docs/architecture.md), [ADRs](docs/architecture-decision-records.md)) | Today on `main` |
|---|---|
| Three UIs + GraphQL API | API is real; **Angular admin** (KYC-060–065), **React customer** (KYC-070–074), **Vue reports** (KYC-080–081) |
| Modular monolith (Identity, Cases, Documents, Audit) | **Layer folders** inside one .NET project; Documents use-cases exist |
| Command/query **services** (no MediatR) | GraphQL / REST call `*Service` classes. MediatR is not planned ([beyond-mvp.md](docs/beyond-mvp.md) §6) |
| GraphQL as the domain API | Cases are GraphQL; **upload/download are dedicated REST**; login/register still have temporary REST twins |
| MinIO for KYC files | Compose + API `IObjectStorage` / MinIO (InMemory in tests) |
| Redis | Compose up, **unused** by the API |
| Module Federation host | Not MVP. W7 spike; real host only if [beyond-mvp.md](docs/beyond-mvp.md) §2 |

**What you can say with confidence:** “Weeks 1–5 delivered the API plus Angular admin review and React customer create → fill → upload → submit. KYC-080–081 add Vue login/shell and a read-only status-count + latest-10 report on the same JWT contract. Remaining Week 6 work is seed data and security hardening.”

## Suggested reading order

1. This file (you are here).
2. [docs/README.STUDY.md](docs/README.STUDY.md) — how committed docs relate; do not duplicate them.
3. [infrastructure/README.STUDY.md](infrastructure/README.STUDY.md) — why Postgres is on `127.0.0.1`.
4. [apps/api/README.STUDY.md](apps/api/README.STUDY.md) — solution vs project vs tests.
5. Follow **one mutation** through [Kyc.Api](apps/api/Kyc.Api/README.STUDY.md) → [GraphQL](apps/api/Kyc.Api/GraphQL/README.STUDY.md) → [Application](apps/api/Kyc.Api/Application/README.STUDY.md) → [Domain](apps/api/Kyc.Api/Domain/README.STUDY.md) → [Data](apps/api/Kyc.Api/Data/README.STUDY.md). Then skim [Documents](apps/api/Kyc.Api/Application/Documents/README.STUDY.md) for the REST upload path.
6. [Tests](apps/api/Kyc.Api.Tests/README.STUDY.md) — especially tenant isolation. This is the sentence isolation conversations hang on.
7. [Workflows](.github/workflows/README.STUDY.md) — what `api-ci` / `angular-ci` / `react-ci` / `vue-ci` prove.
8. Frontends: [angular-admin](apps/angular-admin/README.STUDY.md), [react-customer](apps/react-customer/README.STUDY.md), [vue-reports](apps/vue-reports/README.STUDY.md).

## What to skip

- `bin/`, `obj/`, `node_modules/` if they appear later — build output.
- `.git/` — history, not design.
- Generated EF `*.Designer.cs` until you have read [Migrations/README.STUDY.md](apps/api/Kyc.Api/Data/Migrations/README.STUDY.md).

## Links

- Run the repo: [README.md](README.md)
- API runbook: [apps/api/README.md](apps/api/README.md)
- Frontend-oriented .NET map: [docs/guides/dotnet-api-for-frontend-engineers.md](docs/guides/dotnet-api-for-frontend-engineers.md)
- How to write C# here: [docs/dotnet-code-standards.md](docs/dotnet-code-standards.md)
- [Roadmap](docs/roadmap.md) (W1–W5 done; W6 next)
- [Beyond MVP](docs/beyond-mvp.md) (Redis, MF host, invites — triggers only)
- [ADR-001 monorepo](docs/architecture-decision-records.md)
- [ADR-007 tenant from JWT](docs/architecture-decision-records.md)
- [.NET SDK / global.json](https://learn.microsoft.com/dotnet/core/tools/global-json)
- [Conventional Commits](https://www.conventionalcommits.org/) used by [docs/commits.md](docs/commits.md)
