# KYC API

.NET 10 modular monolith exposing a Hot Chocolate GraphQL API.

## Responsibilities

- Multi-tenant request resolution
- KYC case orchestration
- Identity verification provider integrations
- CQRS with MediatR
- Domain event publishing
- PostgreSQL persistence (EF Core)
- Redis caching and session management

## Getting Started

```bash
dotnet restore
dotnet run
```

GraphQL playground will be available at `http://localhost:5000/graphql`.
