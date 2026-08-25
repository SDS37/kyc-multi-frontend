# KYC Multi-Frontend Platform

> Production-oriented multi-tenant KYC & Compliance platform  
> Demonstrating modern micro-frontends, GraphQL, and resilient .NET architecture.

**Tech Stack**
- **Frontends**: Angular (Shell + Admin), React (Customer Portal), Vue (Reports)
- **API**: Hot Chocolate GraphQL on .NET
- **Backend**: Modular Monolith with CQRS, multi-tenancy and production security patterns
- **Infrastructure**: Docker, PostgreSQL, Redis

This repository is a portfolio project designed to showcase advanced frontend architecture and full-stack capabilities beyond pure frontend development.

## Project Status

🚧 In active development. App folders are placeholders until their stories are implemented.

## Repository structure

```
apps/angular-admin     Angular shell + admin/reviewer portal
apps/react-customer    React customer portal
apps/vue-reports       Vue reports portal
apps/api               .NET GraphQL API
docs/                  Architecture and project documentation
infrastructure/        Docker Compose and local dependencies
```

## Documentation

- [Business Requirements](docs/business-requirements.md)
- [Architecture](docs/architecture.md)
- [Roadmap](docs/roadmap.md)
- [Definition of Done](docs/DoD.md)
- [ADRs](docs/architecture-decision-records.md)
- [How to Commit](docs/commits.md)

## Commit convention

Use [Conventional Commits](docs/commits.md): `type(scope): message`.

Examples: `feat(api): add tenant login`, `docs: add architecture diagrams`.

## Getting Started

(Coming soon)

## License

This project is licensed under the MIT License – see the [LICENSE](LICENSE) file for details.