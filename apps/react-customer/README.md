# React Customer

React **19+** customer portal (`apps/react-customer`). KYC-070 foundation · KYC-071 login · KYC-072 my cases · KYC-073 draft form · KYC-074 document upload.

**How to write this app:** [Frontend code standards](../../docs/frontend-code-standards.md) (shared rules + [React section](../../docs/frontend-code-standards.md#react-appsreact-customer)). Shared `--kyc-*` tokens match Angular admin ([ux-design-tokens.md](../../docs/ux-design-tokens.md)).

## Prerequisites

- Node.js **20.19+** (22 recommended; see `.nvmrc`)
- API at `http://localhost:5295`
- CORS allows `http://localhost:5173`

## Run

```bash
cd apps/react-customer
npm install
npm start
```

App: `http://localhost:5173` → `/login` (guests) or `/cases` (signed in)

## Demo login

`registerTenant` creates **TenantAdmin**. Sign in works for any role; **`createDraftCase` / draft edit / submit / document upload require Customer**.

```json
{ "tenantSlug": "acme", "adminEmail": "admin@acme.example", "adminPassword": "ChangeMe1", "tenantName": "Acme" }
```

Provision a Customer user in the DB for create-draft E2E (no public signup yet).

## Security notes

- List/create/detail/update/submit use JWT `Authorization` — never `skipAuth`
- Document upload is REST multipart (`POST /api/cases/{id}/documents`) with the same JWT (ADR-002)
- Own-cases filter is **API-side**; client never sends `customerUserId` / `tenantId`
- Create input is **title only**; update sends `id` + `title` + `formData` (ADR-007)
- Client blocks non-PDF/PNG/JPG and files over 10 MB before upload

## Component tree (KYC-074)

Living render tree for this app (update when routes change). Smart vs presentational **rules:** [frontend-code-standards](../../docs/frontend-code-standards.md#component-tree-all-frontends). System composition: [architecture.md §3](../../docs/architecture.md#3-frontend-composition).

```mermaid
flowchart TB
  Main["main.tsx"] --> App["App"]
  App --> Login["/login"]
  App --> Shell["CustomerShell"]
  Shell --> Cases["/cases CaseList smart"]
  Shell --> Draft["/cases/:id CaseDraft smart"]
  Cases --> Toolbar["CasesToolbar"]
  Cases --> Table["CaseListTable"]
  Cases --> Dialog["CreateDraftDialog"]
  Cases --> Status["Loading / Empty / LoadError"]
  Draft --> Form["CaseDraftForm"]
  Draft --> Readonly["CaseDraftReadonly"]
  Draft --> DraftStatus["Loading / LoadError"]
  Form --> Docs["CaseDocumentsPane"]
  Readonly --> Docs
```

| Piece | Location |
|---|---|
| Login | `src/auth/login-page/` |
| My cases smart screen | `src/cases/case-list/case-list.tsx` |
| Presentational list UI | `cases-toolbar`, `case-list-table`, `create-draft-dialog`, loading/empty/error |
| Draft smart screen | `src/cases/case-draft/case-draft.tsx` |
| Presentational draft UI | `case-draft-form`, readonly, loading/error |
| Documents pane | `src/cases/case-documents-pane/` |
| Cases API / mappers | `src/cases/cases-api.ts`, `cases.mappers.ts` |
