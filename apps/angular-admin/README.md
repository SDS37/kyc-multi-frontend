# Angular Admin

Placeholder for the Angular shell and admin/reviewer portal.

No application scaffold yet (`package.json`, `angular.json`, and source are not in the repo). Week 4 is KYC-060–064.

## Intended responsibilities (W4)

- Shell chrome and routing for the reviewer / Tenant Admin experience (KYC-064)
- Login (tenant slug + email + password) against GraphQL `login` (KYC-061)
- Case list: title, **customer email**, status, updated date, status filter (KYC-062)
- Case review: form data, documents **with download**, start / approve / reject (KYC-063)

Tenant user and role management is **not** in the API and **not** in KYC-060–064. Do not invent it in this app.

React and Vue stay separate apps for MVP (ADR-005). Auth and cases use the shared GraphQL API; document download is REST `GET /api/cases/{caseId}/documents/{documentId}` with the same JWT.

The API already allows this origin (`Cors:AllowedOrigins` includes `http://localhost:4200`).

When the scaffold exists:

```bash
npm install
npm start
```

Expected URL: `http://localhost:4200`.
