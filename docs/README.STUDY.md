# Study: `docs`

Study tour of this folder. Distinct from the official README. These files **are** the project documentation. This note only tells you **how to read them** as a frontend architect.

**Aligned with:** KYC-081 Vue reports overview (counts + latest 10).

## Purpose

Committed markdown here is the **contract with yourself and reviewers**: what the product is, which decisions are locked, what “done” means. `README.STUDY.md` files elsewhere must **link here**, not fork a second architecture.

## Why these files exist (and when to open which)

| File | Open it when | Do not use it for |
|---|---|---|
| [architecture.md](architecture.md) | You need C4 / request / lifecycle diagrams, frontend composition (apps → shells → API), or **today vs unused Redis / W7 MF** | Exact `dotnet` commands; per-pane UI trees (those live in app READMEs); treating dotted Redis as a live dependency |
| [architecture-decision-records.md](architecture-decision-records.md) | Someone asks “why GraphQL / why not microservices / why tenant in JWT” | Implementation details of a service |
| [roadmap.md](roadmap.md) | Sequencing: W1 identity, W2 GraphQL+cases, W3 documents… | Claiming Redis is in use today (MinIO **is** used for uploads) |
| [beyond-mvp.md](beyond-mvp.md) | After DoD: Redis, MF host, user invite/list, TLS — **triggers**, not a second sprint plan | Treating the wishlist as in-flight W6/W7 work |
| [business-requirements.md](business-requirements.md) | Product scope, roles, KYC meaning | Code structure |
| [DoD.md](DoD.md) | MVP exit criteria (isolation tested, three UIs on one API) | Week-by-week tasks |
| [commits.md](commits.md) | Commit message format | Design |
| [dotnet-code-standards.md](dotnet-code-standards.md) | How to write C# in `apps/api` (layers, errors, tenancy, tests) | Why GraphQL / JWT (ADRs); exact `dotnet` commands |
| [frontend-code-standards.md](frontend-code-standards.md) | How to write UI apps (Hard TS, smart vs presentational **rules**); Angular follows angular.dev plus a filtered [Angular Architects](https://www.angulararchitects.io/en/) slice | Living component-tree mermaids (app READMEs); API runbook; their Agentic UI / Sheriff / MF talks |
| [ux-design-tokens.md](ux-design-tokens.md) | Shared color/spacing tokens + a11y baseline for all UIs (MF-safe) | Framework component APIs |
| [guides/dotnet-api-for-frontend-engineers.md](guides/dotnet-api-for-frontend-engineers.md) | First .NET orientation (csproj vs package.json) | Current field list — it can lag; prefer [apps/api/README.md](../apps/api/README.md) |

There is no `docs/api/` Swagger dump. The GraphQL schema **is** the API doc (IDE in Development).

## Why a `guides/` folder

Guides are **persona-specific** (you: Angular-strong, .NET-new). Architecture/ADRs are persona-neutral. If a guide disagrees with `architecture.md`, architecture + ADRs win — then the guide should be updated in a docs PR, not “fixed” in a study file.

Prefer the API README done-checks and root README status table as the live index; the frontend-oriented guide can lag after each week.

## Angular analog

This folder is the architecture decision log you wish every Angular repo kept: ADRs instead of Slack archaeology. Roadmap is the PI board. DoD is the release checklist.

## Today vs target

Architecture diagrams include Angular/React/Vue and Audit **modules**. API + Angular admin + React customer + Vue reports **overview** (counts + latest 10) are real. Week 6 Playwright smokes are on `main`.

ADR-008: no formal AI context packs for MVP. `README.STUDY.md` files are a **tour of the tree**, not a versioned agent context pack — they do not contradict that ADR. Keep them aligned with the code; delete them from the repo when they are no longer useful.

## What to skip

- Re-reading ADRs 001–008 every session — 001, 002, 003, 007 are the ones you will quote weekly.
- Treating roadmap dates as calendar commitments; they are sequence.

## Links

- Root tour: [../README.STUDY.md](../README.STUDY.md)
- [C4 model](https://c4model.com/) (architecture.md is C4-ish: context, container, then backend modules)
- [Documenting architecture decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) (ADR origin)
- [Mermaid](https://mermaid.js.org/) (diagrams in architecture.md)
