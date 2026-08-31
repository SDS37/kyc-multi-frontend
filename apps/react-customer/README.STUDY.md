# Study: `apps/react-customer`

Study tour of this folder. Official README: [README.md](README.md).

**Aligned with:** KYC-070 foundation on `feat/kyc-070-react-foundation`.

## Purpose

Customer portal (Week 5, KYC-070–074): create a KYC case, fill FormData, upload documents, submit, watch status. Different framework on purpose (portfolio + GraphQL-as-shared-contract). Same JWT and schema as Angular admin.

## Why a separate app

ADR-004/005: three stacks, one API. Isolation of **tenants** is a backend property; isolation of **apps** is a product/portfolio property.

## Foundation map

| Concern | Where |
|---|---|
| Router | `src/app-router.tsx` |
| Shell | `src/layout/customer-shell.tsx` |
| Config | `src/config/app-config.ts` |
| Token | `src/auth/token-storage.ts` |
| GraphQL fetch | `src/shared/http.ts` (`graphqlRequest`, `apiFetch`) |
| Messages | `src/shared/ui.messages.ts` |
| Standards | [frontend-code-standards — React](../../docs/frontend-code-standards.md#react-appsreact-customer) |

## What it will consume (product stories)

| Need | API |
|---|---|
| Login | GraphQL `login` (tenant slug + email + password) |
| Create / edit draft | `createDraftCase` / `updateDraftCase` — Customer only |
| Submit | `submitCase` |
| List / detail | `cases` / `case(id)` |
| Upload | REST `POST /api/cases/{id}/documents` |

Do not send `customerUserId` or `tenantId` on create (ADR-007).

## Angular-expert trap

Do not share a TypeScript library of GraphQL documents across Angular and React for MVP. ADR-005 accepts duplicated login/token handling.

## What to skip

- Approve/reject UI (Angular)
- Apollo / Redux until a story needs them
- Public Customer signup until identity product clarifies provisioning

## Links

- [README.md](README.md)
- [react.dev](https://react.dev/)
- [ADR-005](../../docs/architecture-decision-records.md)
- [Cases STUDY](../api/Kyc.Api/Application/Cases/README.STUDY.md)
