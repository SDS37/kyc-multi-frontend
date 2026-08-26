# KYC Multi-Frontend Platform

> Production-oriented multi-tenant KYC & Compliance platform.
> Angular admin shell, React customer portal, Vue reports, and a .NET GraphQL API.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

This monorepo is a portfolio project. Three independent frontends share one GraphQL API and auth contract. Module Federation is a later spike, not a Week 1 requirement.

## Tech Stack

| Layer | Technology |
|---|---|
| Admin / Shell | Angular |
| Customer Portal | React |
| Reports | Vue |
| API | Hot Chocolate GraphQL on .NET |
| Backend | Modular monolith, CQRS, multi-tenancy |
| Data | PostgreSQL, Redis, MinIO |
| Local run | Docker Compose |

## Repository structure

```
kyc-multi-frontend/
├── apps/
│   ├── angular-admin/     # Angular shell + admin/reviewer portal (not scaffolded yet)
│   ├── react-customer/    # React customer portal (not scaffolded yet)
│   ├── vue-reports/       # Vue reports portal (not scaffolded yet)
│   └── api/               # .NET API host + EF Core (GraphQL in KYC-020)
├── docs/
├── infrastructure/
│   ├── docker-compose.yml
│   └── .env.example
├── .editorconfig
├── .gitignore
├── LICENSE
└── README.md
```

## Getting Started

**Prerequisites:** Docker Desktop and the .NET 10 SDK (for the API).

Angular, React, and Vue apps are not scaffolded yet. The API host in `apps/api` builds and talks to Compose PostgreSQL. GraphQL comes in KYC-020.

### 1. Clone

```bash
git clone https://github.com/SDS37/kyc-multi-frontend.git
cd kyc-multi-frontend
```

### 2. Start PostgreSQL, Redis, and MinIO

```bash
cp infrastructure/.env.example infrastructure/.env
docker compose -f infrastructure/docker-compose.yml up -d
```

| Service | Address | Default credentials |
|---|---|---|
| PostgreSQL | `127.0.0.1:5432` | user `kyc`, password `changeme`, database `kyc_db` |
| Redis | `127.0.0.1:6379` | password `changeme` |
| MinIO API | `127.0.0.1:9000` | user `minio`, password `changeme1` |
| MinIO console | `127.0.0.1:9001` | same as API |

These defaults are for local development only. Change them in `infrastructure/.env`. Data persists in Docker named volumes.

Stop with `docker compose -f infrastructure/docker-compose.yml down`.

### 3. Run the API

See [apps/api/README.md](apps/api/README.md) (restore, migrate, `dotnet run`). HTTP: `http://localhost:5295`.

## Architecture

See [docs/architecture.md](docs/architecture.md) and [ADRs](docs/architecture-decision-records.md).

```mermaid
flowchart TB
    subgraph Clients["Browser"]
        Admin["Angular Admin"]
        Customer["React Customer"]
        Reports["Vue Reports"]
    end

    GQL["GraphQL API<br/>.NET / Hot Chocolate"]
    DB[(PostgreSQL)]
    Cache[(Redis)]
    Files[(MinIO)]

    Admin --> GQL
    Customer --> GQL
    Reports --> GQL
    GQL --> DB
    GQL --> Cache
    GQL --> Files
```

- **Multi-tenancy:** tenant and role come from the JWT, never from client-supplied IDs (ADR-007).
- **CQRS:** commands and queries are separate in the application layer.
- **GraphQL:** one schema for all three clients (ADR-002).
- **Frontends:** independent apps for MVP; Module Federation is deferred (ADR-005).
- **Files:** KYC documents go to MinIO (ADR-006).

## Documentation

- [Business Requirements](docs/business-requirements.md)
- [Architecture](docs/architecture.md)
- [Roadmap](docs/roadmap.md)
- [Definition of Done](docs/DoD.md)
- [Architecture Decision Records](docs/architecture-decision-records.md)
- [Commit Convention](docs/commits.md)
- [API runbook](apps/api/README.md)
- [.NET API for frontend engineers](docs/guides/dotnet-api-for-frontend-engineers.md)

## Commit convention

Use [Conventional Commits](docs/commits.md): `type(scope): message`.

Examples: `feat(api): add tenant login`, `docs: add architecture diagrams`.

## License

This project is licensed under the [MIT License](LICENSE).
