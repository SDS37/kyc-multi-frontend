### Definition of Done for Project 1 MVP

- Customer can create, fill, upload, submit
- Reviewer can review, approve/reject
- Tenant isolation tested (JWT + EF filters; Case inherits `ITenantScoped`)
- 3 frontends consume the same GraphQL API (identity via GraphQL `login` / `registerTenant`; temporary REST allow-listed until clients migrate)
- Architecture diagrams published (target vs today called out)
- README allows a colleague to run the system locally (Compose + API runbook + `api-ci`)
