# Architecture – KYC Multi-Frontend (MVP)

This document describes the **target architecture** for the MVP. Implementation follows the [roadmap](roadmap.md): identity and infrastructure first, then GraphQL, cases, documents, and the three frontends.

**Today on `main`:** Compose (Postgres / Redis / MinIO); .NET API with EF Core, Tenant/User/Case, JWT login, fail-closed `ICurrentTenant` / `ICurrentUser` + `ITenantScoped` EF filters; Hot Chocolate `/graphql` + `/health` (GraphQL IDE in Development); GraphQL deny-by-default JWT auth with anonymous `login` / `registerTenant` (KYC-021); field-level Reviewer stub + Customer `createDraftCase` (KYC-022 / KYC-031); temporary REST register/login on the same allow-list; `apps/api/Kyc.Api.sln` + tests; GitHub Actions `api-ci` (KYC-102). Remaining case lifecycle GraphQL and UI apps are not built yet.

**MVP frontends (ADR-005):** three independent apps against the same GraphQL API. Section 3 is the **Week 7 target** (Angular shell composing remotes); it is not required for the first release.

## 1. System Context

```mermaid
flowchart LR
    Admin["Tenant Admin / Reviewer<br/>Angular Admin"]
    Customer["Customer<br/>React Portal"]
    Reports["Tenant Admin / Reviewer<br/>Vue Reports"]
    GQL["GraphQL API<br/>Hot Chocolate + .NET"]
    DB[(PostgreSQL)]
    Cache[(Redis)]
    Files[(Object Storage)]

    Admin --> GQL
    Customer --> GQL
    Reports --> GQL
    GQL --> DB
    GQL --> Cache
    GQL --> Files
```

## 2. Container View

```mermaid
flowchart TB
    subgraph Clients
        Shell["Angular Shell + Admin"]
        ReactApp["React Customer Portal"]
        VueApp["Vue Reports Portal"]
    end

    subgraph Backend
        API["API Host / Hot Chocolate"]
        Identity["Identity and Tenancy"]
        Cases["Cases Module"]
        DocsMod["Documents Module"]
        Audit["Audit Module"]
    end

    subgraph Data
        PG[(PostgreSQL)]
        Redis[(Redis)]
        Minio[(MinIO)]
    end

    Shell --> API
    ReactApp --> API
    VueApp --> API
    API --> Identity
    API --> Cases
    API --> DocsMod
    API --> Audit
    Identity --> PG
    Cases --> PG
    DocsMod --> PG
    DocsMod --> Minio
    Audit --> PG
    API --> Redis
```

## 3. Frontend Composition (target after Module Federation spike)

MVP ships three separate apps. The diagram below is the intended shell composition if the Week 7 federation spike succeeds (ADR-005).

```mermaid
flowchart TB
    Shell["Angular Host / Shell"]
    Admin["Angular Admin"]
    Customer["React Customer"]
    Reports["Vue Reports"]

    Shell --> Admin
    Shell --> Customer
    Shell --> Reports
```

## 4. Backend Module Structure

```mermaid
flowchart TB
    subgraph API["API Host"]
        GQL["Hot Chocolate GraphQL"]
        Middleware["Auth / Tenant Middleware"]
    end

    subgraph Modules
        Identity["Identity & Tenancy"]
        Cases["Cases Module"]
        Documents["Documents Module"]
        Audit["Audit Module"]
    end

    subgraph CrossCutting["Cross-Cutting"]
        CQRS["MediatR CQRS"]
        Events["Domain Events"]
    end

    GQL --> Middleware
    Middleware --> Identity
    Middleware --> Cases
    Middleware --> Documents
    Middleware --> Audit
    Cases --> CQRS
    Documents --> CQRS
    Audit --> CQRS
    CQRS --> Events
```

## 5. Request Flow

```mermaid
sequenceDiagram
    participant UI as Frontend
    participant GQL as GraphQL
    participant Auth as Auth and Tenant
    participant App as Application
    participant DB as PostgreSQL

    UI->>GQL: Query or Mutation + JWT
    GQL->>Auth: Validate token and tenant
    Auth-->>GQL: User and Tenant context
    GQL->>App: Command or Query
    App->>DB: Persist or Read
    DB-->>App: Data
    App-->>GQL: Result
    GQL-->>UI: Typed payload
```

## 6. Case Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Submitted: Customer submits
    Submitted --> InReview: Reviewer starts review
    InReview --> Approved: Reviewer approves
    InReview --> Rejected: Reviewer rejects
    Approved --> [*]
    Rejected --> [*]
```