# React Customer

Placeholder for the React customer portal.

No application scaffold yet (`package.json` is not in the repo).

## Intended responsibilities

- Customer login against the shared auth contract
- Create and fill a KYC case
- Upload documents and submit the case
- See case status (`Draft` → `Submitted` → `InReview` → `Approved` / `Rejected`)
- Consume the shared GraphQL API once KYC-020 lands (temporary REST auth exists today)

When the scaffold exists:

```bash
npm install
npm start
```

Expected URL: `http://localhost:3000`.
