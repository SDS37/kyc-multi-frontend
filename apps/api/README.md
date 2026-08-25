# API

Placeholder for the .NET GraphQL API (Hot Chocolate).

No application scaffold yet (no `.csproj` / `.sln`). Do not run `dotnet` here until that exists.

## Intended responsibilities

- One GraphQL schema for Angular, React, and Vue
- Identity and tenancy (JWT carries tenant and role)
- Cases, documents, and audit modules
- PostgreSQL via EF Core; KYC files in MinIO
- CQRS in the application layer

When the scaffold exists:

```bash
dotnet restore
dotnet run
```

Expected GraphQL endpoint: `http://localhost:5000/graphql`.
