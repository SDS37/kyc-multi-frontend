# Business Requirements – MVP

**Product**: KYC Compliance Platform
**Version**: MVP
**Last updated**: 2026-09-02
**Status**: Aligned with accepted ADRs; W1–W6 delivered on `main` (API + Angular admin + React customer + Vue reports + Playwright smokes). KYC-091 (local CORS + basic headers), KYC-093 (auth abuse controls), KYC-094 (frontend 429 + optional login captcha), KYC-095 (post-094 review punch-list), KYC-100 (runbook), KYC-101 (demo seed), KYC-110 (Playwright smokes), and [#108](https://github.com/SDS37/kyc-multi-frontend/issues/108) (CSP / HTTPS redirect) are done.

## 1. Vision

A multi-tenant platform that allows companies to manage KYC (Know Your Customer) cases, collect information and documents from end customers, review them, and maintain a clear audit trail — with strong security and strict tenant isolation.

## 2. Target Users

| Role | Description |
|---|---|
| Tenant Admin | Same review mutations as Reviewer (Angular). Read-only Vue overview. **User invite/list is not in MVP** ([beyond-mvp.md](beyond-mvp.md)). |
| Reviewer | Reviews and decides on KYC cases (Angular) |
| Customer | Submits KYC information and documents (React) |

## 3. Core Business Requirements (MVP)

### 3.1 Multi-Tenancy
- The system must support multiple independent companies (tenants).
- Data belonging to one tenant must never be accessible by another tenant.
- Each tenant has its own users and cases.

### 3.2 Authentication & Authorization
- Users must authenticate securely.
- The system supports at least three roles: Tenant Admin, Reviewer, and Customer.
- Authorization must be enforced based on the user’s role and tenant.

### 3.3 Case Management
- A Customer can create a new KYC case.
- A case follows a simple status lifecycle:
  `Draft → Submitted → InReview → Approved / Rejected`
  (UI copy may say “In Review”; the domain status name is `InReview`.)
- Reviewers can change the status and add internal comments.
- Both Customers and Reviewers can view case details (according to permissions).

### 3.4 Information Collection
- A case must support collecting structured information.
- The system should allow flexible capture of data (basic fields + possibility to extend).

### 3.5 Document Management
- Customers can upload documents related to a case.
- Authorized users (mainly Reviewers) can download the documents.
- Documents must be stored securely (MinIO in MVP) and only accessible within the same tenant.

### 3.6 Audit Trail
- Important actions (status changes, document uploads, key updates) must be recorded.
- The audit log must show who performed the action and when.

### 3.7 Basic Overview
- Users must be able to see a list of cases relevant to their role.
- Basic filtering by status must be available.

### 3.8 Security & Compliance Basics
- Strict tenant isolation is mandatory.
- Authentication and authorization must be properly implemented.
- Basic production security practices must be followed (input validation, secure document access, etc.).

## 4. Out of Scope for MVP

- Advanced customizable workflows / approval chains
- Billing and subscription management
- Advanced analytics and dashboards
- Real email/SMS notifications (can be mocked)
- Native mobile applications
- Automated document verification / OCR / liveness
- MFA
- Multi-language runtime (English `*.messages.ts` catalogs only; second locale is [beyond-mvp.md](beyond-mvp.md))
- Module Federation as a product host (Week 7 **spike** only; keep three apps if it fails)
- User management UI / invite API beyond seed ([KYC-101](https://github.com/SDS37/kyc-multi-frontend/issues/42) / [beyond-mvp.md](beyond-mvp.md))
- Redis-backed cache, rate limits, or JWT denylist (Compose Redis is unused)
- JWT refresh / server-side logout

Production-shaped follow-ups after DoD: [beyond-mvp.md](beyond-mvp.md).

## 5. Success Criteria for MVP

- A complete happy path works end-to-end:
  Customer creates a case → uploads documents → submits → Reviewer reviews and approves/rejects.
- Tenant isolation is correctly enforced.
- The solution has a clean architecture that can evolve into a real product (see [beyond-mvp.md](beyond-mvp.md) — not in this DoD).
