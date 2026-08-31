# Study: `apps/react-customer`

**Aligned with:** KYC-070–072 on `feat/kyc-072-react-my-cases`.

## Purpose

Customer portal: sign in, list own cases, create drafts (FormData edit/submit = KYC-073+).

## Map

| Concern | Where |
|---|---|
| List + create | `src/cases/case-list/` |
| GraphQL | `src/cases/cases-api.ts` (`cases`, `createDraftCase`) |
| Pure parse | `src/cases/cases.mappers.ts` |
| Guards | `src/auth/route-guards.tsx` |

## Angular differences

Admin list is a review queue (includes `customerEmail`, no create). Customer list drops email, adds **New case** → `createDraftCase` → `/cases/:id`.

## Links

- [README.md](README.md)
- [Issue #33](https://github.com/SDS37/kyc-multi-frontend/issues/33)
- [Angular case-list](../angular-admin/src/app/cases/case-list/)
