# Architecture Decision Records

This file records the main architecture decisions for the KYC multi-frontend MVP.

| ID | Title | Status |
|----|--------|--------|
| 001 | Use a monorepo | Accepted |
| 002 | Use GraphQL as the API layer | Accepted |
| 003 | Use a modular monolith, not microservices | Accepted |
| 004 | Use Angular for the admin application | Accepted |
| 005 | Do not block MVP on Module Federation | Accepted |
| 006 | Use PostgreSQL and MinIO for MVP persistence | Accepted |
| 007 | Resolve tenant from the JWT, never from client input | Accepted |
| 008 | Defer formal AI context-engineering packs for MVP | Accepted |

---

## ADR-001: Use a monorepo

Date: 2026-08-24  
Status: Accepted

### Context
The monorepo is intended to hold an Angular admin app, a React customer portal, a Vue reports app, a .NET GraphQL API, infrastructure, and documentation. This is a solo portfolio project. **Today:** Compose + a .NET API with temporary REST identity endpoints; the three UI apps are placeholders only.

### Decision
Keep all code in one repository: `kyc-multi-frontend`.

### Alternatives
- One repository per app and one for the API
- Separate frontend monorepo and backend repository

### Consequences
- One place for architecture, ADRs, stories and code
- Simpler local setup and Docker Compose
- Easier for reviewers to understand the whole system
- Repository will grow
- Independent versioning of each app is weaker

---

## ADR-002: Use GraphQL as the API layer

Date: 2026-08-24  
Status: Accepted

### Context
Three frontends need different slices of the same KYC data. The API must act as a shared contract and as middleware between clients and the .NET backend.

### Decision
Use Hot Chocolate GraphQL as the single public API for all frontends.

Until KYC-020 lands, the only client-facing surface is temporary REST (`POST /api/register-tenant`, `POST /api/login`). That interim does not change the GraphQL target.

### Alternatives
- REST + OpenAPI
- One BFF per frontend
- gRPC for browsers

### Consequences
- Each client can request only the data it needs
- One schema for Angular, React and Vue
- Good fit for field-level authorization later
- More setup than REST
- Need DataLoaders to avoid N+1 queries
- File upload/download may still use a dedicated path
- Temporary REST must be retired or folded into GraphQL when KYC-020 ships

---

## ADR-003: Use a modular monolith, not microservices

Date: 2026-08-24  
Status: Accepted

### Context
The domain has clear modules: Identity, Cases, Documents and Audit. The project must show production patterns without operational overhead that a solo developer cannot maintain.

### Decision
Build one .NET deployable with internal modules and clear boundaries. Use CQRS inside the application layer. Do not split into microservices for MVP.

### Alternatives
- Microservices from day one
- Classic single-layer MVC / single project

### Consequences
- Faster local development
- Easier transactions and consistent tenant rules
- Boundaries can later become services
- Must stay disciplined about module dependencies
- Not a full distributed-systems showcase

---

## ADR-004: Use Angular for the admin application

Date: 2026-08-24  
Status: Accepted

### Context
The admin/review experience is the most complex UI: case list, review, documents, status transitions and layout. Angular is the author's strongest frontend skill.

### Decision
Implement the admin portal in Angular. React is used for the customer portal. Vue is used for the simple reports view.

### Alternatives
- React as admin
- Blazor as admin
- One framework for all three apps

### Consequences
- Best use of existing Angular expertise
- Stronger portfolio signal for frontend architecture
- Different clients justify GraphQL as a shared contract
- Three UI stacks increase build and setup cost
- Shared UI components are harder

---

## ADR-005: Do not block MVP on Module Federation

Date: 2026-08-24  
Status: Accepted

### Context
The original target is a shell that loads Angular, React and Vue remotes. Module Federation adds setup risk and can delay the business MVP.

### Decision
Ship three independent apps against the same GraphQL API and auth contract. Treat Module Federation as a Week 7 spike. If it is not stable, keep separate apps for MVP.

### Alternatives
- Module Federation from week 1
- Single-page app in one framework only

### Consequences
- MVP can focus on domain, security and GraphQL
- Each app can still demonstrate its stack
- No runtime composition in the first release
- Some duplicated login and token handling

---

## ADR-006: Use PostgreSQL and MinIO for MVP persistence

Date: 2026-08-24  
Status: Accepted

### Context
The system needs relational data for tenants, users, cases and audit, plus file storage for KYC documents. Local development must be simple.

### Decision
Use PostgreSQL for business data and MinIO as S3-compatible object storage. Redis may be used later for cache or tokens, but is not required for the first vertical slice.

### Alternatives
- SQL Server only
- Store files in the database
- Azure Blob Storage from the start

### Consequences
- Easy Docker Compose setup
- MinIO keeps the door open for S3/Azure Blob later
- Relational model fits cases, roles and audit
- Extra container to run
- Production cloud storage is not configured in MVP

---

## ADR-007: Resolve tenant from the JWT, never from client input

Date: 2026-08-24  
Status: Accepted

### Context
This is a multi-tenant KYC system. If a client can send `tenantId` in a mutation and that value is trusted, tenant isolation can be broken.

### Decision
Put `tenant_id` and `role` in the JWT after login. All queries and commands take tenant and user identity from the authenticated context. Client-supplied tenant IDs are ignored for authorized operations.

### Alternatives
- Tenant in every GraphQL argument
- Tenant from subdomain only
- Shared database without query filters

### Consequences
- Stronger isolation
- Simpler authorization rules
- Better auditability
- Login must include tenant slug or equivalent
- Token handling must be consistent in all three frontends
- EF global filters on `ITenantScoped` fail closed without a JWT tenant (KYC-014); GraphQL must keep the same rule (KYC-021)
---

## ADR-008: Defer formal AI context-engineering packs for MVP

Date: 2026-08-27  
Status: Accepted

### Context
Formal **AI context engineering** (in the sense popularized by practitioners such as Daniel Glejzner / ACE) treats agent instructions as production infrastructure: owned, versioned, allow-listed, validated before use, and measurable — not ad-hoc chat dumps or unowned rule files scattered across tools.

That approach is valuable when many people or agents share outdated context, or when scope drift from agents is a repeated failure mode. This repository is a **solo portfolio MVP** with:

- GitHub stories and acceptance criteria
- ADRs, roadmap, and API runbooks
- Small PRs and Conventional Commits
- Established code patterns (`User`, `ITenantScoped`, GraphQL deny-by-default auth)

Those already provide enough scoped context to ship Week 2+ stories (e.g. [KYC-030](https://github.com/SDS37/kyc-multi-frontend/issues/13)) without a separate context supply chain.

### Decision
**Do not** introduce a formal context-pack system (e.g. `.context/packs/...` with VERSION / SOURCES / CONSTRAINTS) or ACE-style governance as an MVP deliverable.

Continue to steer agents with:

1. The issue AC and notes
2. “Mirror existing patterns; do not invent parallel mechanisms”
3. Proof via tests called out in the story

Revisit formal packs only if agent scope drift becomes a real, recurring cost.

### Alternatives
- Adopt ACE-style packs for every story from KYC-030 onward
- Heavy global Cursor/Claude rule sets as the primary contract
- No written ADRs — rely on chat memory only

### Consequences
- Less process overhead; focus stays on Cases / Documents / UIs
- Context quality still depends on keeping issues and ADRs accurate
- No versioned “what reached the agent” audit trail
- If multiple contributors or heavy agent automation appear later, ADR-008 should be revisited and packs (or equivalent) considered
