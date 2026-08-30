# Frontend code standards

Conventions for UI apps under `apps/` (Angular admin, React customer, Vue reports). These complement the ADRs and roadmap — write new frontend work to match them.

**Not this file:** API runbook ([apps/api/README.md](../apps/api/README.md)), .NET C# rules ([dotnet-code-standards.md](dotnet-code-standards.md)), or *why* three separate apps / GraphQL exist ([architecture-decision-records.md](architecture-decision-records.md)). If this file and an ADR disagree, the ADR wins.

**Authority for Angular-specific rules** (first match wins):

1. **ADRs** — product decisions (separate apps, no MF for MVP, no AI context packs)
2. **[angular.dev](https://angular.dev)** — APIs, Style Guide, HTTP / routing / DI
3. **This file** — what we enforce in this repo
4. **[Angular Architects](https://www.angulararchitects.io/en/)** (Manfred Steyer & team) — architecture *patterns* we adopt below; not their workshop stack

This file is not a copy of angular.dev or of the Angular Architects blog.

## Shared (all frontends)

| Topic | Rule |
|---|---|
| Apps | Three independent apps (ADR-005): `apps/angular-admin`, `apps/react-customer`, `apps/vue-reports` — no Module Federation for MVP |
| API contract | GraphQL for domain reads/writes; document **download** is REST with the same JWT; do not invent extra BFF routes |
| Auth | Store the access token after login; send `Authorization: Bearer <token>` on authenticated API calls; never put `tenant_id` / role in client-supplied request bodies for authorized ops (ADR-007) |
| Config | GraphQL (and REST API) base URLs come from environment / build config — not hard-coded production hosts |
| Design tokens | Shared `@kyc/design-tokens` CSS variables for color, spacing, type, focus — see [ux-design-tokens.md](ux-design-tokens.md). Map each app’s UI kit to tokens; do not fork palettes per app |
| Angular UI kit | **Angular Material** (+ CDK) themed to `@kyc/design-tokens`. **Do not** add the Bootstrap CSS framework (or a second spacing/color system) |
| **Models files** | Feature DTOs / form maps / domain errors in `*.models.ts` (not inside services/components). Same convention in every UI app. |
| **Functional style** | Prefer FP **at every app level**, expressed as pure **functions** (mappers, derived state, transforms). Side effects only at I/O edges. See below. |
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

### Functional style / purity (all frontends)

Prefer a **functional style across every level of the app** — UI, feature services, HTTP clients, and shared helpers. The unit of that style is the **pure function** (same inputs → same outputs; no hidden I/O): put transforms in named functions (often `*.mappers.ts`), keep templates/`computed` thin, and push side effects to the edges.

This is **not** a mandate for `fp-ts` or rewriting Angular as a functional framework — DI, classes, RxJS, and signals stay first-class. It **is** a mandate to think functionally **everywhere**, not only inside mapper files.

| Level | Prefer | Avoid |
|---|---|---|
| **Shared / `*.mappers.ts`** | Pure functions: normalize, parse GraphQL → DTO, error map, labels, URL/filter parse | HTTP, router, storage inside “helpers” |
| **Services** | Compose pure functions in `map`; I/O only via HttpClient / `tap` / storage APIs | Dense callbacks that parse **and** write tokens / navigate |
| **Components** | Signals + `computed` / pure helpers for derived UI; immutable `set` / `update` | In-place mutation; business parsing copied into the class |
| **Templates** | Bind signals / simple calls | Heavy branching or formatting logic |
| **Collections** | `filter` / `map` / `flatMap` / `reduce` that return new values | `for` / `forEach` that `.push` into an outer array (same job, less clear) |
| **Immutability** | New arrays/objects (`[...xs]`, `{ ...o }`, `toSorted` / copy-then-sort); signal `set`/`update` with new values | `.push` / `.splice` / in-place `.sort()` / mutating fields on shared objects |

```typescript
// ✅ GOOD — pure function at the transform; side effect at the edge (any layer)
map((body) => parseLoginSuccess(body)),
tap((login) => tokens.setAccessToken(login.accessToken)),

// ❌ BAD — side effect buried inside a function that should stay pure
map((body) => {
  const login = …;
  tokens.setAccessToken(login.accessToken);
  return login;
}),
```

Prefer **array methods over imperative loops** when transforming lists:

```typescript
// ✅ GOOD — filter + map (no mutable accumulator)
const knownFields: CaseFormField[] = CASE_FORM_FIELD_KEYS.filter(
  (key): boolean => key in record,
).map(
  (key): CaseFormField => ({
    key,
    label: CASE_FORM_FIELD_LABELS[key],
    value: formatFormFieldValue(record[key]),
  }),
);

// ❌ BAD — for / forEach + push (imperative accumulation)
const fields: CaseFormField[] = [];
for (const key of CASE_FORM_FIELD_KEYS) {
  if (!(key in record)) continue;
  fields.push({ key, label: CASE_FORM_FIELD_LABELS[key], value: … });
}
```

**Immutability (whole app):** treat data as replaceable, not editable in place.

| Prefer | Avoid |
|---|---|
| `[...items, next]` / `items.filter(…)` / `items.map(…)` | `items.push(next)` / `items.splice(…)` |
| `[...keys].sort(…)` or `keys.toSorted(…)` | `keys.sort(…)` on an array you still share |
| `{ ...detail, status: next }` | `detail.status = next` |
| `signal.set(next)` / `signal.update(prev => …new…)` | Mutate the object/array already held by a signal |

`for…of` is still OK when you need early `break` or non-transform control flow. Prefer `filter`/`map`/`reduce` for “list in → list out.” Do **not** treat `forEach` as more functional than `for` — both are imperative when they mutate. Avoid `.push` as the default way to build lists.

Enforce this for the **whole** frontend surface (`angular-admin` now; React/Vue when scaffolded). Feature layout still helps: `*.models.ts` (shapes) + `*.mappers.ts` (pure functions) + `*.service.ts` (I/O) + components (wiring + signals).

## Angular (`apps/angular-admin`)

Follow the official [Angular Style Guide](https://angular.dev/style-guide) and related guides. When in doubt, **prefer consistency with the file and with angular.dev**.

### Versions and scaffold

| Setting | Value | Do not |
|---|---|---|
| Angular | Latest stable major (22+ at KYC-060 writing) | Pin an outdated major “because the issue once said 19” |
| Components | **Standalone** only | Add NgModules for app features |
| App entry | `src/main.ts` (`bootstrapApplication`) | Alternate entry layouts without a reason |
| UI kit | Angular Material + `@kyc/design-tokens` (`material-theme.scss`) | Bootstrap CSS, competing CSS frameworks, or a second hard-coded palette |
| UI code | Under `src/` | Dump feature code at the app root |

### Naming and structure ([Style Guide](https://angular.dev/style-guide))

- Hyphenated file names matching the TypeScript symbol: `user-profile.ts` ↔ `UserProfile`
- Co-locate component `.ts` / `.html` / styles; tests as `*.spec.ts` beside the code
- Organize by **feature area**, not by type folders (`components/`, `services/`)
- One primary concept per file (one component / directive / service unless a small cohesive pair)
- **`*.models.ts` everywhere (app-wide):** every feature keeps DTOs, form control maps, domain errors, and feature GraphQL wire bodies in a models file — e.g. `auth/auth.models.ts`, `cases/cases.models.ts`, `config/config.models.ts`. Cross-feature wire bits (e.g. `GraphqlError`) live under `shared/*.models.ts`. Injectable services and components **import** models; they do **not** declare exported interfaces/types inline.
- **`*.mappers.ts` for pure functions (app-wide):** normalize, parse GraphQL → DTO, map errors, parse filters/URLs. FP is expected at **all** app levels (see table above); mappers are the main home for shared pure functions. Services own HTTP/`tap` side effects. See [Functional style / purity](#functional-style--purity-all-frontends).

### Angular Architects practices (filtered for this app)

Source: [angulararchitects.io](https://www.angulararchitects.io/en/) — [blog](https://www.angulararchitects.io/en/blog/), [presentations](https://www.angulararchitects.io/en/presentations/), and publications from Manfred Steyer (strategic design / Sheriff / signals) and team. Their public work in 2025–26 also covers **Agentic UI**, A2UI, AG-UI, CopilotKit, and AI-agent harnesses. **Those are out of scope** for this portfolio MVP (and conflict with [ADR-008](architecture-decision-records.md)).

Their useful thesis for us: **structure the app so illegal dependencies cannot be expressed**, then use Angular 22’s signal APIs for the reactive loop — without importing their airline-sized matrix, Nx, Sheriff, or tsarch.

#### What we adopt now

| Their idea | How it lands here |
|---|---|
| Strategic design / feature slices, not technical layer folders | Keep `auth/`, `cases/`, `config/`, `shared/` — do not add `components/` + `services/` buckets |
| Architecture matrix layers: feature → ui → data → util | Smart route components and feature services may use presentational UI, data services, and mappers. **UI (dumb) must not call HTTP or inject feature services.** Mappers stay pure (`util`). |
| Contexts talk only to themselves + shared | `cases` must not import `auth` login/form/mappers. Token, guards, and interceptor are **shared infrastructure** (treat `TokenStorage` / `authGuard` as allowed from any feature). New shared UI goes in `shared/`, not copied per feature. |
| Smart vs dumb (Steyer / Nrwl categories) | Route screens (`login`, `case-list`, later review) are smart: wiring, navigation, services. Extract **presentational** pieces (`-card`, empty/error panes) with `input()` / `output()` only — no `HttpClient`, no `CasesService`. |
| Data access is not in the template | GraphQL/REST stay in `*.service.ts` (their “client”). Do **not** rename existing services to `-client.ts`. Components do not `http.post` GraphQL. |
| No store-to-store; orchestrate instead | If two stores appear later, a feature service / coordinator composes them with `computed`. Stores must not inject each other. |
| Resource API is the signal-era load path ([Angular 22](https://www.angulararchitects.io/en/blog/angular-22-the-most-important-new-features-at-a-glance/)) | For **new** signal-driven reads, prefer `rxResource` (or `resource`) that calls the existing typed `*.service.ts`. We speak **GraphQL**, so do **not** use `httpResource` for `/graphql` (it is REST-shaped). Existing `switchMap` + signals on the case list is fine until that screen is touched. |
| Signal Forms are stable in Angular 22 | **New** forms (review reject reason, later customer drafts) use `@angular/forms/signals` (`form` + schema). Keep login on Reactive Forms until a story rewrites it — do not churn KYC-061 for fashion. Split large forms into subform components; put reusable schemas next to `*.models.ts`. |
| `OnPush` is the Angular 22 default | Do not set `ChangeDetectionStrategy.Eager` on new components. Rely on signals / inputs. See [OnPush and the component tree](#onpush-and-the-component-tree). |
| Deterministic client code owns structure | Same as [Functional style](#functional-style--purity-all-frontends): parse/order/label in mappers; the UI does not invent GraphQL codes or status order. |

#### OnPush and the component tree

Angular 22 components default to **OnPush**. That means a component is checked when something it cares about changes (signal read, `input()` / bound `@Input`, template event, explicit mark) — **not** whenever any ancestor runs change detection. Pair that with **immutable signal updates** so OnPush actually sees the change.

Current `angular-admin` tree (lazy routes under the root outlet):

```mermaid
flowchart TB
  subgraph root["Change-detection root"]
    App["App<br/><code>app-root</code><br/>OnPush"]
  end

  App --> Outlet["RouterOutlet"]

  Outlet --> Login["Login<br/><code>app-login</code><br/>OnPush<br/>signals: submitting, formError"]
  Outlet --> CaseList["CaseList<br/><code>app-case-list</code><br/>OnPush<br/>signals: items, filter, loading, …"]
  Outlet --> CaseReview["CaseReview<br/><code>app-case-review</code><br/>OnPush<br/>signals: detail, actions, …"]

  CaseReview -.->|"optional later"| Presentational["Presentational children<br/><code>input()</code> / <code>output()</code> only"]

  classDef dirty fill:#dbeafe,stroke:#2563eb,color:#0f172a
  classDef idle fill:#f1f5f9,stroke:#64748b,color:#0f172a
  class CaseList,CaseReview dirty
  class App,Login,Outlet idle
```

**Isolation example:** `CaseList` does `items.set(page.items)` after a GraphQL load.

| Component | Checked? | Why |
|---|---|---|
| `CaseList` | Yes | It read/wrote signals the template binds to |
| `CaseReview` | Only when mounted on `/cases/:id` and its signals change | Lazy route — separate subtree |
| Presentational child (later) | Only if its `input()` values changed | OnPush + new input references |
| `App` | No need to re-check the whole app for list data | Outlet host is not dirtied by the list’s signal write |
| `Login` | Not in the tree on `/cases` | Lazy route — not mounted |

```text
Eager (old default mental model): any event → walk large parts of the tree
OnPush + signals (this app):       signal/input dirtiness → check that subtree only
```

Rules that keep isolation real:

- Update signals with **new** values (`set` / `update`); do not mutate arrays/objects in place
- Presentational pieces take `input()` / `output()` — parents pass new object/array references when data changes
- Do **not** switch a screen to `ChangeDetectionStrategy.Eager` to “make CD work”; fix the signal/input update instead

#### Building-block access (lightweight, no Sheriff)

```
smart route / feature service
    → presentational UI (inputs/outputs only)
    → *.service.ts (HTTP)
    → *.mappers.ts / *.models.ts
```

- Only the data service talks to the API.
- Smart components may inject the data service **or** a small feature service that holds signals / `rxResource`. Either is OK at this size. Do not add a Store *and* let the component also call the service for the same read.
- Presentational components do not inject stores or HTTP.
- Shell / `app.config` may import any feature (their “root” exception).

#### What we explicitly defer

| Their tool / talk | Why not now |
|---|---|
| [Sheriff](https://www.angulararchitects.io/en/blog/modern-architectures-with-angular-part-1-strategic-design-with-sheriff-and-standalone-components/) lint rules | Needs ESLint; we declined ESLint for W4. Two features do not need a domain matrix. Revisit if `cases` + more domains start deep-importing each other. |
| [tsarch](https://www.angulararchitects.io/en/blog/architecture-beyond-layers-tsarch-for-ai-agents/) ArchUnit-style tests | Same: executable naming rules for `-store` / `-client` / `-coordinator`. Convention in this file is enough. |
| Nx + path aliases (`@demo/ticketing/data`) | One CLI app. Relative imports stay. |
| Multi-context DDD folders (`domains/booking/feature-*`) | Reviewer admin is one product. `auth` + `cases` is the map. |
| NgRx Toolkit Mutation API + full-cycle SignalStore | Same rule as [Signals and client state](#signals-and-client-state): after list↔detail sharing hurts. |
| `@Service()`, `injectAsync` / `onIdle` | Optional Angular 22 sugar. Use `injectAsync` only for a heavy optional library, not for `CasesService`. |
| Incremental hydration | Not an SSR app. |

#### What we never take from that site for MVP

- Module Federation / micro-frontend host (ADR-005 — W7 spike only)
- Agentic UI, A2UI, AG-UI, CopilotKit, MCP Apps, “AI coding agent” stop-hooks
- Formal `AGENTS.md` architecture packs as a second source of truth (ADR-008). **This file** stays the contract.
- Classic global NgRx Store “because enterprise”

If an Angular Architects article and an ADR disagree, the ADR wins. If it disagrees with angular.dev on an API, angular.dev wins.

### Dependency injection

- Prefer the [`inject()`](https://angular.dev/style-guide#prefer-the-inject-function-over-constructor-parameter-injection) function over constructor parameter injection for new code
- Keep components focused on presentation; move API / token / GraphQL orchestration into injectable services
- **No component constructor bodies for app logic.** Prefer `inject()` field initializers and lifecycle hooks (`ngOnInit`, …). Constructors run outside Angular’s usual DI/lifecycle ergonomics — do not subscribe, start HTTP, or wire RxJS pipelines there. If you need `takeUntilDestroyed` outside an injection context, pass an injected `DestroyRef`.

### Components and templates

- Prefer `input()` / `output()` (or documented modern equivalents) with `readonly` where Angular owns the binding
- Use `protected` for members only consumed by the template
- Prefer `[class]` / `[style]` bindings over `ngClass` / `ngStyle`
- Name event handlers for the **action** (`saveCase()`), not the DOM event (`handleClick()`)
- Keep lifecycle hooks thin; implement the lifecycle interfaces (`OnInit`, etc.) when used
- Avoid heavy logic in templates — move complexity into the class (e.g. `computed`)
- Wire RxJS / first-load requests in `ngOnInit` (or later hooks), not in `constructor()`

```typescript
// ✅ GOOD — inject fields; subscribe in ngOnInit
private readonly destroyRef: DestroyRef = inject(DestroyRef);

ngOnInit(): void {
  this.reloadRequests.pipe(…, takeUntilDestroyed(this.destroyRef)).subscribe(…);
  this.reload();
}

// ❌ BAD — business / RxJS wiring in constructor
constructor() {
  this.reloadRequests.pipe(…).subscribe(…);
}
```

### Signals and client state

Prefer Angular **signals** (`signal` / `computed` / `WritableSignal`) for UI and feature state that should be explicit and readable (loading flags, form-level errors, list filter, items). Keep GraphQL/HTTP in injectable services; components (or a small feature service) own the signals that the template reads.

**Do not** add NgRx **SignalStore** (or classic NgRx store) for MVP by default. Shipped surface today: login (KYC-061) + case list (KYC-062). Case review (KYC-063+) should stay component/feature-service signals until list↔detail sharing actually hurts.

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
- GraphQL endpoint (and REST API origin for document download) live in Angular `environment` / `APP_CONFIG` (injected at app start)

### Routing

- Use the Angular Router with a clear `routes` config (`provideRouter`)
- Lazy-load feature routes (`login`, `cases`, later review) where it keeps the foundation bundle small
- Guard authenticated areas with `authGuard`; send guests to login with `guestGuard` (KYC-061+)

### TypeScript and formatting

- **Hard TypeScript from day one** (shared rule above): keep and extend strictness; do not weaken without cause
- Angular admin baseline includes `strict`, `noUncheckedIndexedAccess`, `noUnusedLocals`, `noUnusedParameters`, plus `strictTemplates` / `strictInjectionParameters` / `strictInputAccessModifiers`
- Match repo [`.editorconfig`](../.editorconfig) for the stack (Angular CLI defaults for the app are fine if consistent inside `apps/angular-admin`)

### What Angular must not do in MVP

- Call MinIO or Postgres directly — only the .NET API
- Trust client-supplied tenant id for authorization
- Reintroduce NgModule-based feature modules “for familiarity”
- Add the **Bootstrap CSS** framework alongside Material; hard-code a second color/spacing system instead of `@kyc/design-tokens`
- Add NgRx SignalStore / global store “for scale” before shared list↔detail case state actually needs it (see Signals and client state above)
- Declare exported DTOs / form maps / domain errors inside services or components instead of `*.models.ts`
- Bury storage / router / HTTP side effects inside pure mappers (see Functional style / purity)
- Prefer `for` / `forEach` + `.push` for list transforms when `filter` / `map` / `reduce` would do (see Functional style / purity → Collections)
- Mutate arrays/objects in place (`.push`, `.splice`, in-place `.sort`, assigning fields on shared DTOs) — prefer immutable copies (see Functional style / purity → Immutability)
- Put component business logic or RxJS subscriptions in `constructor()` — use `ngOnInit` / lifecycle + `inject()` fields instead
- Import another feature’s internals (e.g. `cases` → `auth.mappers` / login form). Use `shared/` or the allowed auth infrastructure listed above
- Put GraphQL/`HttpClient` in presentational (`-card` / pane) components
- Use `httpResource` for GraphQL; use Sheriff, tsarch, Nx, or Module Federation “because Angular Architects”
- Rewrite working Reactive Forms to Signal Forms with no product story
- Set `ChangeDetectionStrategy.Eager` on new screens without a measured reason
- Add Agentic UI / CopilotKit / A2UI from the 2026 Angular Architects talks

## React and Vue (later)

Apply the **Shared** section (including design tokens and a11y). Framework-specific subsections will be added with KYC-070 / KYC-080 foundations, still pointing at each framework’s official docs as the authority. Import `@kyc/design-tokens/tokens.css` at app start the same way Angular does.

## Links

- [UX design tokens & accessibility](ux-design-tokens.md)
- [Angular Style Guide](https://angular.dev/style-guide)
- [HttpClient interceptors](https://angular.dev/guide/http/interceptors)
- [Standalone](https://angular.dev/guide/components) / routing docs on [angular.dev](https://angular.dev)
- Angular Architects (patterns only): [site](https://www.angulararchitects.io/en/), [blog](https://www.angulararchitects.io/en/blog/), [presentations](https://www.angulararchitects.io/en/presentations/), [Angular 22 features](https://www.angulararchitects.io/en/blog/angular-22-the-most-important-new-features-at-a-glance/), [Sheriff / strategic design](https://www.angulararchitects.io/en/blog/modern-architectures-with-angular-part-1-strategic-design-with-sheriff-and-standalone-components/), [Signal Forms](https://www.angulararchitects.io/en/blog/all-about-angulars-new-signal-forms/), [Signal Store and architecture](https://www.angulararchitects.io/en/blog/the-ngrx-signal-store-and-your-architecture/), [Resource + forms + store](https://www.angulararchitects.io/en/blog/full-cycle-reativity-in-angular-signal-forms-signal-store-resources-mutation-api/)
- [ADR-004](architecture-decision-records.md) (Angular admin), [ADR-005](architecture-decision-records.md) (separate apps), [ADR-007](architecture-decision-records.md) (tenant in JWT)
- App slot: [apps/angular-admin/README.md](../apps/angular-admin/README.md)
- Tokens package: [packages/design-tokens](../packages/design-tokens/)
