# Architecture – KYC Multi-Frontend (MVP)

## 1. System Context

```mermaid
flowchart LR
    Admin["Tenant Admin / Reviewer<br/>Angular Admin"]
    Customer["Customer<br/>React Portal"]
    Reports["Reports User<br/>Vue Portal"]

    GQL["GraphQL API<br/>Hot Chocolate + .NET"]

    DB[(PostgreSQL)]
    Cache[(Redis)]
    Files[(Object Storage<br/>MinIO / local)]

    Admin --> GQL
    Customer --> GQL
    Reports --> GQL
    GQL --> DB
    GQL --> Cache
    GQL --> Files

flowchart TB
    subgraph Clients
        Shell["Angular Shell + Admin"]
        ReactApp["React Customer Portal"]
        VueApp["Vue Reports Portal"]
    end

    subgraph Backend[".NET Modular Monolith"]
        API["API Host<br/>Hot Chocolate"]
        Identity["Identity & Tenancy Module"]
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