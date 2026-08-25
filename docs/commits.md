## Type, When to use

- feat, A new feature
- fix, A bug fix
- docs, Documentation only
- style, "Formatting, missing semicolons, etc. (no logic)"
- refactor,C ode change that neither fixes a bug nor adds a feature
- perf,P erformance improvement
- test, Adding or correcting tests
- build, Changes to build system or dependencies
- ci, CI configuration
- chore, "Other changes (tooling, configs, etc.)"
- revert, Reverts a previous commit

## Recommended Scopes

- api
- angular
- react
- vue
- docs
- infra
- auth
- cases
- shared

## Examples

- feat(api): add multi-tenancy support with tenant isolation
- feat(angular): implement case list page with filtering
- feat(react): add document upload component
- fix(api): correct authorization check on document download
- docs: add business requirements for MVP
- chore: add comprehensive .gitignore
- refactor(api): extract case status transitions into domain service
- ci: add basic GitHub Actions workflow