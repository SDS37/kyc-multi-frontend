# Study: `apps/react-customer`

**Aligned with:** KYC-070–074 on `feat/kyc-074-react-document-upload`.

## Purpose

Customer portal: sign in, list own cases, create/edit drafts, submit, upload documents (PDF/PNG/JPG ≤10 MB).

## Map

| Concern | Where |
|---|---|
| Smart list screen | `src/cases/case-list/case-list.tsx` |
| Smart draft screen | `src/cases/case-draft/case-draft.tsx` |
| Presentational leaves | toolbar, table, create dialog, draft form/readonly, documents pane, loading/empty/error |
| GraphQL | `src/cases/cases-api.ts` (`cases`, `createDraftCase`, `case`+`documents`, `updateDraftCase`, `submitCase`) |
| REST upload | `uploadDocument` → `POST /api/cases/{id}/documents` |
| Pure parse | `src/cases/cases.mappers.ts` |
| Guards | `src/auth/route-guards.tsx` |

## Angular differences

Admin list is a review queue (includes `customerEmail`, no create). Customer owns draft edit/submit and document **upload**; admin documents pane is list+download only.

## Links

- [README.md](README.md)
- [Issue #35](https://github.com/SDS37/kyc-multi-frontend/issues/35) (KYC-074)
- [Issue #34](https://github.com/SDS37/kyc-multi-frontend/issues/34) (KYC-073)
- [Angular documents pane](../angular-admin/src/app/cases/case-documents-pane/)
