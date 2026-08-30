# Frontend code standards

Conventions for UI apps under `apps/` (Angular admin, React customer, Vue reports). These complement the ADRs and roadmap — write new frontend work to match them.

**Not this file:** API runbook ([apps/api/README.md](../apps/api/README.md)), .NET C# rules ([dotnet-code-standards.md](dotnet-code-standards.md)), or *why* three separate apps / GraphQL exist ([architecture-decision-records.md](architecture-decision-records.md)). If this file and an ADR disagree, the ADR wins.

**Authority for Angular-specific rules:** prefer [angular.dev](https://angular.dev) (especially the [Style Guide](https://angular.dev/style-guide) and HTTP / routing / DI guides). This file selects the rules we enforce in this repo; it is not a full copy of the docs.

## Shared (all frontends)

| Topic | Rule |
|---|---|
| Apps | Three independent apps (ADR-005): `apps/angular-admin`, `apps/react-customer`, `apps/vue-reports` — no Module Federation for MVP |
| API contract | GraphQL for domain reads/writes; document **download** is REST with the same JWT; do not invent extra BFF routes |
| Auth | Store the access token after login; send `Authorization: Bearer <token>` on authenticated API calls; never put `tenant_id` / role in client-supplied request bodies for authorized ops (ADR-007) |
| Config | GraphQL (and REST API) base URLs come from environment / build config — not hard-coded production hosts |
| Design tokens | Shared `@kyc/design-tokens` CSS variables for color, spacing, type, focus — see [ux-design-tokens.md](ux-design-tokens.md). Map Material/other UI kits to tokens; do not fork palettes per app |
| Accessibility | WCAG 2.2 AA intent + WAI-ARIA across Angular/React/Vue (same `aria-*` platform). Labels, focus visible (`--kyc-focus-ring`), errors not by color alone — details in [ux-design-tokens.md](ux-design-tokens.md) |
| **Hard TypeScript** | **Strict TS from the first file** in every UI app — see below. No “loose then tighten later.” |
| Secrets | No real passwords or JWT secrets in source; local demo credentials stay in README / `.env.example` only |
| Commits | [Conventional Commits](commits.md) with scopes like `angular`, `react`, `vue`, `docs` |
| CI | Angular admin: GitHub Actions `angular-ci` (`npm ci`, build, `test:ci`) when `apps/angular-admin/**` changes |

Do not invent Tenant user-management UIs in W4–W6 — that API does not exist yet (`apps/angular-admin/README.md`).

### Hard TypeScript (all frontends)

Ship with a hard type-checker from day one. Scaffold and PRs must keep it that way.

| Do | Do not |
|---|---|
| `strict: true` (and framework template/DI strictness where it exists) | Turn off `strict` / `strictTemplates` to unblock a feature |
| Explicit interfaces for GraphQL/HTTP bodies and domain models | `any`, untyped `object`, or “just cast it” |
| **Explicit types on locals / fields / constants** (`const x: string = …`) | Rely on inference alone for non-trivial values (harder to read; silent widen/drift) |
| `unknown` + narrow in `catch` / error callbacks | Empty `catch` or `catch (e: any)` |
| Extra flags when practical: `noUncheckedIndexedAccess`, `noUnusedLocals`, `noUnusedParameters` | `@ts-ignore` / `@ts-expect-error` without a short justification |
| Typed test fixtures (`ActivatedRouteSnapshot`, `HttpTestingController`, …) | `as any` / `as never` to silence tests |

Annotate variables for readability and to shrink the mistake gap — not only public APIs:

```typescript
const token: string | null = storage.getAccessToken();
const target: string = returnUrl ?? '/cases';
private readonly router: Router = inject(Router);
```

New React/Vue apps must adopt the same baseline when scaffolded (KYC-070 / KYC-080) — copy Angular’s hard `compilerOptions` intent, not a softer Vite default.

## Angular (`apps/angular-admin`)

Follow the official [Angular Style Guide](https://angular.dev/style-guide) and related guides. When in doubt, **prefer consistency with the file and with angular.dev**.

### Versions and scaffold

| Setting | Value | Do not |
|---|---|---|
| Angular | Latest stable major (22+ at KYC-060 writing) | Pin an outdated major “because the issue once said 19” |
| Components | **Standalone** only | Add NgModules for app features |
| Bootstrap | `src/main.ts` | Alternate entry layouts without a reason |
| UI code | Under `src/` | Dump feature code at the app root |

### Naming and structure ([Style Guide](https://angular.dev/style-guide))

- Hyphenated file names matching the TypeScript symbol: `user-profile.ts` ↔ `UserProfile`
- Co-locate component `.ts` / `.html` / styles; tests as `*.spec.ts` beside the code
- Organize by **feature area**, not by type folders (`components/`, `services/`)
- One primary concept per file (one component / directive / service unless a small cohesive pair)

### Dependency injection

- Prefer the [`inject()`](https://angular.dev/style-guide#prefer-the-inject-function-over-constructor-parameter-injection) function over constructor parameter injection for new code
- Keep components focused on presentation; move API / token / GraphQL orchestration into injectable services

### Components and templates

- Prefer `input()` / `output()` (or documented modern equivalents) with `readonly` where Angular owns the binding
- Use `protected` for members only consumed by the template
- Prefer `[class]` / `[style]` bindings over `ngClass` / `ngStyle`
- Name event handlers for the **action** (`saveCase()`), not the DOM event (`handleClick()`)
- Keep lifecycle hooks thin; implement the lifecycle interfaces (`OnInit`, etc.) when used
- Avoid heavy logic in templates — move complexity into the class (e.g. `computed`)

### Signals and client state

Prefer Angular **signals** (`signal` / `computed` / `WritableSignal`) for UI and feature state that should be explicit and readable (loading flags, form-level errors, list filter, items). Keep GraphQL/HTTP in injectable services; components (or a small feature service) own the signals that the template reads.

**Do not** add NgRx **SignalStore** (or classic NgRx store) for MVP by default. This app stays small through KYC-062 (case list): login + list + later review is still a handful of feature services, not a multi-domain shared state graph.

Practical rule (MVP):

- Use signals everywhere they clarify UI / feature state
- Introduce SignalStore **only if** KYC-063 / KYC-064 (or later) starts sharing **non-trivial** case state across list + detail + actions **and** a plain injectable service with signals gets messy
- Until then, SignalStore is premature architecture for this portfolio MVP

#### When SignalStore is *not* needed (MVP)

| Situation | Prefer |
|---|---|
| Login form flags (`submitting`, `formError`) | Component-local signals |
| KYC-062 case list alone (filter + items + loading / empty) | One feature service or component signals + HTTP service |
| “We might need a store later” with no shared state yet | Do nothing — revisit after MVP when list↔detail sharing appears |

#### After MVP — when to extend with SignalStore

Revisit this section when **several screens share one mutable domain model** with rules that do not belong in any single component. In `angular-admin`, that usually means list + detail + review actions owning the same case queue/detail.

Signals that you have outgrown a plain service:

- List filter / status must stay coherent after opening a case and approving or rejecting
- Detail updates should refresh or patch the list row without duplicating refetch/patch logic in two places
- Optimistic UI (e.g. approve disables row + detail, rolls back on GraphQL error)
- Multiple consumers of the same signals (shell badge “N in review”, list, detail)

**Rule of thumb:** if you can name one store (e.g. `CaseWorkspaceStore`) with clear methods (`setStatusFilter`, `loadList`, `loadDetail`, `approve`, `reject`) used by **two or more routes**, SignalStore starts earning its keep. One route → keep signals on the component or a thin feature service.

Suggested post-MVP shape (sketch only — implement when the pain is real):

1. Keep GraphQL HTTP in a thin `CasesApi` / `CasesService` (no UI state there)
2. Add `@ngrx/signals` SignalStore as `CaseWorkspaceStore` (`providedIn` feature or route) holding list + selected detail + filter + pending action flags
3. List and detail components inject the store; shell may read a small derived signal (e.g. in-review count)
4. Do **not** migrate login or unrelated features into that store

### HTTP, auth, and config

- Provide HttpClient with **functional** interceptors: `provideHttpClient(withInterceptors([authInterceptor]))` ([official recommendation](https://angular.dev/guide/http/interceptors))
- Auth interceptor: read the token from a small token/auth service, `req.clone({ headers: … })`, attach `Authorization` when a token exists; skip public calls (login) via URL or `HttpContext` if needed
- Token storage: a dedicated service (MVP may use `localStorage` or `sessionStorage`); do not scatter `localStorage.getItem` across features
- GraphQL endpoint (and API origin for document download) live in Angular `environment` / application config used at bootstrap

### Routing

- Use the Angular Router with a clear `routes` config (`provideRouter`)
- Lazy-load feature routes where it keeps the foundation bundle small
- Guard authenticated areas once login exists (KYC-061+); foundation may stub a shell route first

### TypeScript and formatting

- **Hard TypeScript from day one** (shared rule above): keep and extend strictness; do not weaken without cause
- Angular admin baseline includes `strict`, `noUncheckedIndexedAccess`, `noUnusedLocals`, `noUnusedParameters`, plus `strictTemplates` / `strictInjectionParameters` / `strictInputAccessModifiers`
- Match repo [`.editorconfig`](../.editorconfig) for the stack (Angular CLI defaults for the app are fine if consistent inside `apps/angular-admin`)

### What Angular must not do in MVP

- Call MinIO or Postgres directly — only the .NET API
- Trust client-supplied tenant id for authorization
- Reintroduce NgModule-based feature modules “for familiarity”
- Add Bootstrap alongside Material; hard-code a second color/spacing system instead of `@kyc/design-tokens`
- Add NgRx SignalStore / global store “for scale” before shared list↔detail case state actually needs it (see Signals and client state above)

## React and Vue (later)

Apply the **Shared** section (including design tokens and a11y). Framework-specific subsections will be added with KYC-070 / KYC-080 foundations, still pointing at each framework’s official docs as the authority. Import `@kyc/design-tokens/tokens.css` at app bootstrap the same way Angular does.

## Links

- [UX design tokens & accessibility](ux-design-tokens.md)
- [Angular Style Guide](https://angular.dev/style-guide)
- [HttpClient interceptors](https://angular.dev/guide/http/interceptors)
- [Standalone](https://angular.dev/guide/components) / routing docs on [angular.dev](https://angular.dev)
- [ADR-004](architecture-decision-records.md) (Angular admin), [ADR-005](architecture-decision-records.md) (separate apps), [ADR-007](architecture-decision-records.md) (tenant in JWT)
- App slot: [apps/angular-admin/README.md](../apps/angular-admin/README.md)
- Tokens package: [packages/design-tokens](../packages/design-tokens/)
