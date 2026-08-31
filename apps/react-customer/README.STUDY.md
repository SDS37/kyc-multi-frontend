# Study: `apps/react-customer`

Study tour of this folder. Official README: [README.md](README.md).

**Aligned with:** KYC-070 foundation + KYC-071 login on `feat/kyc-071-react-login`.

## Purpose

Customer portal (Week 5, KYC-070–074): sign in, create a KYC case, fill FormData, upload documents, submit, watch status. Same JWT and GraphQL schema as Angular admin; different product surface.

## Foundation + login map

| Concern | Where |
|---|---|
| Router / guards | `src/app-router.tsx`, `src/auth/route-guards.tsx` |
| Login UI | `src/auth/login-page/` (Angular-parity `--kyc-*` layout) |
| Auth contract | `src/auth/auth.models.ts`, `auth.mappers.ts`, `login-api.ts` |
| Shell | `src/layout/customer-shell.tsx` |
| Cases stub | `src/cases/cases-placeholder/` until KYC-072 |
| Standards | [frontend-code-standards — React](../../docs/frontend-code-standards.md#react-appsreact-customer) |

## Demo credentials

`registerTenant` creates **TenantAdmin** only (`acme` / `admin@acme.example` / `ChangeMe1`). Login works for any role once the user exists. Customer-only APIs need a Customer user provisioned outside this app (no public signup).

## What to skip

- Approve/reject UI (Angular)
- Apollo / Redux until a story needs them
- Public Customer signup

## Links

- [README.md](README.md)
- [react.dev](https://react.dev/)
- [ADR-005](../../docs/architecture-decision-records.md)
- [Angular login](../angular-admin/src/app/auth/login/)
