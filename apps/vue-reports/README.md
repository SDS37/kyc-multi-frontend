# Vue Reports

Placeholder for the Vue reports portal.

No application scaffold yet (`package.json` / Vite config are not in the repo).

## Intended responsibilities

- Read-only view of cases relevant to the tenant
- Basic filtering by status
- Keep the first version to a single page if time is tight (see roadmap)
- Read against the shared GraphQL API (cases list already on `main`)

When the scaffold exists:

```bash
npm install
npm run dev
```

Expected URL: `http://localhost:5174` (avoid colliding with React customer on `5173`).
