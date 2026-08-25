# Business Requirements — MVP Scope

**Project:** Multi-Tenant KYC Compliance Platform
**Version:** 1.0 — MVP
**Date:** 2026-08-24
**Status:** Draft

---

## 1. Purpose

This document defines the Minimum Viable Product (MVP) scope for the KYC Compliance Platform. The platform enables financial institutions (tenants) to digitally onboard customers, verify their identities, assess risk, and maintain an auditable compliance record — all in a multi-tenant SaaS model.

---

## 2. Stakeholders

| Role | Responsibility |
|---|---|
| Compliance Officer | Reviews and approves KYC cases |
| Customer | Completes the KYC self-service journey |
| Platform Admin | Manages tenants, users, and system configuration |
| Auditor | Reviews audit logs and compliance reports |

---

## 3. MVP Feature Scope

### 3.1 Multi-Tenancy

- [ ] Tenant registration and provisioning
- [ ] Tenant isolation at the data layer (schema-per-tenant or row-level security)
- [ ] Tenant-specific branding configuration (logo, colours)
- [ ] Tenant admin user with scoped permissions

### 3.2 Customer Onboarding

- [ ] Customer self-registration (email + password)
- [ ] KYC form: personal details (name, date of birth, nationality, address)
- [ ] Identity document upload (passport, national ID, driver's licence)
- [ ] Selfie / liveness check (integration with third-party provider placeholder)
- [ ] Onboarding status tracking (Pending → In Review → Approved / Rejected)
- [ ] Email notification on status change

### 3.3 KYC Case Management (Admin)

- [ ] Case queue with filtering by status, risk level, and date
- [ ] Case detail view with document previews
- [ ] Manual approval / rejection with mandatory notes
- [ ] Risk score display (calculated by rules engine)
- [ ] Escalation workflow (escalate to senior reviewer)

### 3.4 Risk Assessment

- [ ] Configurable risk rules per tenant (e.g., high-risk nationalities, PEP flags)
- [ ] Automatic risk score calculation on case submission
- [ ] Risk level classification: Low / Medium / High / Unacceptable

### 3.5 Audit & Compliance Reporting

- [ ] Immutable audit log for all case actions (who, what, when)
- [ ] Compliance summary report per tenant (cases processed, approval rate, avg. time-to-decision)
- [ ] Exportable report (CSV / PDF)
- [ ] Data retention policy configuration

### 3.6 Authentication & Authorisation

- [ ] JWT-based authentication
- [ ] Role-based access control (RBAC): Platform Admin, Tenant Admin, Compliance Officer, Customer
- [ ] MFA for compliance officer and admin roles

### 3.7 API

- [ ] GraphQL API (Hot Chocolate) for all frontend interactions
- [ ] Mutations: register, submit KYC, approve/reject case, update settings
- [ ] Queries: tenant info, case list, case detail, reports
- [ ] Subscriptions: real-time case status updates

---

## 4. Out of Scope for MVP

- Automated document OCR (manual review only)
- Payment integrations
- Native mobile applications
- Advanced AML transaction monitoring
- SWIFT / FATF third-party data feeds
- BI / data warehouse integrations

---

## 5. Non-Functional Requirements

| Requirement | Target |
|---|---|
| Availability | 99.9% uptime |
| Response time (p95) | < 500 ms for GraphQL queries |
| Security | OWASP Top 10 compliance; encrypted data at rest and in transit |
| Scalability | Horizontal scaling via containerisation |
| Data residency | Configurable per tenant (EU / US) |
| Audit retention | Minimum 7 years |

---

## 6. Assumptions

- Third-party identity verification providers will expose REST APIs (adapters built per provider).
- All tenants operate under the same regulatory framework for MVP; jurisdiction-specific rules are post-MVP.
- PostgreSQL row-level security is sufficient for tenant isolation in MVP.

---

## 7. Success Criteria

- A new tenant can be provisioned and have their first KYC case reviewed within one business day of platform deployment.
- Compliance officers can process cases without requiring direct database access.
- All case actions are captured in the audit log and available in the compliance report.
