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
    Files[(Object Storage)]

    Admin --> GQL
    Customer --> GQL
    Reports --> GQL
    GQL --> DB
    GQL --> Cache
    GQL --> Files
