# Angular Admin

Placeholder for the Angular shell and admin/reviewer portal.

No application scaffold yet (`package.json`, `angular.json`, and source are not in the repo).

## Intended responsibilities

- Shell chrome and routing for the admin experience
- Case list, review, and status transitions
- Tenant user and role management (Tenant Admin / Reviewer)

React and Vue stay separate apps for MVP (ADR-005). They are not loaded as remotes here yet. Auth and cases will use the shared GraphQL API (KYC-020+); temporary REST register/login exists on the API today.

When the scaffold exists:

```bash
npm install
npm start
```

Expected URL: `http://localhost:4200`.
