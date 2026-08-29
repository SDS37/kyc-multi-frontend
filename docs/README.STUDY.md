# Study: `docs`

Study tour of this folder. Distinct from the official README. These files **are** the project documentation. This note only tells you **how to read them** as a frontend architect.

**Aligned with:** `main` after Week 2.

## Purpose

Committed markdown here is the **contract with yourself and reviewers**: what the product is, which decisions are locked, what “done” means. `README.STUDY.md` files elsewhere must **link here**, not fork a second architecture.

## Why these files exist (and when to open which)

| File | Open it when | Do not use it for |
|---|---|---|
| [architecture.md](architecture.md) | You need C4 / request / lifecycle diagrams or target vs today | Exact `dotnet` commands |
| [architecture-decision-records.md](architecture-decision-records.md) | Someone asks “why GraphQL / why not microservices / why tenant in JWT” | Implementation details of a service |
| [roadmap.md](roadmap.md) | Sequencing: W1 identity, W2 GraphQL+cases, W3 documents… | Claiming Redis is in use today |
| [business-requirements.md](business-requirements.md) | Product scope, roles, KYC meaning | Code structure |
| [DoD.md](DoD.md) | MVP exit criteria (isolation tested, three UIs on one API) | Week-by-week tasks |
| [commits.md](commits.md) | Commit message format | Design |
| [guides/dotnet-api-for-frontend-engineers.md](guides/dotnet-api-for-frontend-engineers.md) | First .NET orientation (csproj vs package.json) | Current field list — it can lag; prefer [apps/api/README.md](../apps/api/README.md) |

There is no `docs/api/` Swagger dump. The GraphQL schema **is** the API doc (IDE in Development).

## Why a `guides/` folder

Guides are **persona-specific** (you: Angular-strong, .NET-new). Architecture/ADRs are persona-neutral. If a guide disagrees with `architecture.md`, architecture + ADRs win — then the guide should be updated in a docs PR, not “fixed” in a study file.

After Week 2, expect the guide’s “still ahead” row to still mention case GraphQL as future. **That lag is real.** Cases queries/mutations are on `main`. Use the API README done-checks as the live index.

## Angular analog

This folder is the architecture decision log you wish every Angular repo kept: ADRs instead of Slack archaeology. Roadmap is the PI board. DoD is the release checklist.

## Today vs target

Architecture diagrams include Angular/React/Vue and Documents/Audit **modules** that are not code yet. The doc itself says “target.” Citing those boxes as implemented is the main way to sound wrong in a review.

ADR-008: no formal AI context packs for MVP. `README.STUDY.md` files are a **tour of the tree**, not a versioned agent context pack — they do not contradict that ADR. Keep them aligned with the code; delete them from the repo when they are no longer useful.

## What to skip

- Re-reading ADRs 001–008 every session — 001, 002, 003, 007 are the ones you will quote weekly.
- Treating roadmap dates as calendar commitments; they are sequence.

## Links

- Root tour: [../README.STUDY.md](../README.STUDY.md)
- [C4 model](https://c4model.com/) (architecture.md is C4-ish: context, container, then backend modules)
- [Documenting architecture decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) (ADR origin)
- [Mermaid](https://mermaid.js.org/) (diagrams in architecture.md)
