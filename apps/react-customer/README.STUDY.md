# Study: `apps/react-customer`

**Aligned with:** KYC-070–073 on `feat/kyc-073-react-draft-form`.

## Purpose

Customer portal: sign in, list own cases, create drafts, edit FormData, submit.

## Map

| Concern | Where |
|---|---|
| Smart list screen | `src/cases/case-list/case-list.tsx` |
| Smart draft screen | `src/cases/case-draft/case-draft.tsx` |
| Presentational leaves | toolbar, table, create dialog, draft form/readonly, loading/empty/error |
| GraphQL | `src/cases/cases-api.ts` (`cases`, `createDraftCase`, `case`, `updateDraftCase`, `submitCase`) |
| Pure parse | `src/cases/cases.mappers.ts` |
| Guards | `src/auth/route-guards.tsx` |

## Angular differences

Admin list is a review queue (includes `customerEmail`, no create). Customer list drops email, adds **New case** → `createDraftCase` → `/cases/:id` draft editor. Admin reviews submitted cases; customers own draft edit/submit.

## Links

- [README.md](README.md)
- [Issue #34](https://github.com/SDS37/kyc-multi-frontend/issues/34) (KYC-073)
- [Issue #33](https://github.com/SDS37/kyc-multi-frontend/issues/33) (KYC-072)
- [Angular case-list](../angular-admin/src/app/cases/case-list/)
