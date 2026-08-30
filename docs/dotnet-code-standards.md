# .NET code standards

Conventions for `apps/api` (the only .NET code in this repo today). These are the rules already implied by the code, ADRs, and tests — write new work to match them.

**Not this file:** how to restore / migrate / run ([apps/api/README.md](../apps/api/README.md)), first-week .NET orientation ([guides/dotnet-api-for-frontend-engineers.md](guides/dotnet-api-for-frontend-engineers.md)), or *why* GraphQL / JWT tenancy exist ([architecture-decision-records.md](architecture-decision-records.md)). If this file and an ADR disagree, the ADR wins.

## Project settings

| Setting | Value | Do not |
|---|---|---|
| Target | `net10.0`, SDK pinned in [`global.json`](../global.json) | Add a second TFM or bump the SDK without changing `global.json` |
| Nullable | enabled | Suppress `CS860x` to “make it compile” |
| Implicit usings | enabled | Re-add `using System;` / `using System.Linq;` unless the type is not covered |
| Formatting | [`.editorconfig`](../.editorconfig): 4 spaces for C# / csproj, 120 columns | Mix tabs or 2-space C# |
| Solution | `apps/api/Kyc.Api.sln` — host + tests only | Add a third project unless an ADR says so |

ADR-003 is a **modular monolith inside one project**. Folders are the modules. Do not split Identity / Cases / Documents into extra `.csproj` files for MVP.

## Language style

- File-scoped namespaces: `namespace Kyc.Api.Application.Cases;`
- Namespace matches folder: `Domain/Cases/Case.cs` → `Kyc.Api.Domain.Cases`
- `sealed` on application services, infrastructure types, and test classes that do not need inheritance
- Primary constructors for DI (`CreateDraftCaseService(AppDbContext db, ICurrentTenant currentTenant, …)`)
- `record` for request/response DTOs (`LoginRequest`, `CaseResponse`)
- `async` / `Task` for anything that hits I/O; always accept `CancellationToken cancellationToken = default` and pass it to EF / HTTP / storage
- UTC timestamps: `DateTimeOffset.UtcNow`
- New ids: `Guid.NewGuid()` (never from the client for tenant or owner)
- Collection expressions where they already appear (`["Title is required."]`)
- XML `<summary>` on public GraphQL types and non-obvious domain invariants — not on every private helper

```csharp
// Prefer
public sealed class ListCasesService(AppDbContext db, ICurrentTenant currentTenant, ICurrentUser currentUser)

// Avoid
public class ListCasesService
{
    public ListCasesService(AppDbContext db, …) { … }
}
```

## Layers

Keep each folder’s job. New code goes in the existing layer, not a parallel one.

| Folder | Owns | Must not contain |
|---|---|---|
| `Domain/` | Entities, enums, `ITenantScoped` | HTTP, GraphQL, JWT, EF attributes, table names |
| `Application/` | One use-case (or small cluster) per class; validation; status transitions; JWT-derived ids | Schema mapping, middleware, migrations |
| `Data/` | `AppDbContext`, `IEntityTypeConfiguration`, migrations | Business rules, GraphQL types |
| `GraphQL/` | Thin `Query` / `Mutation`: authorize, call a service, map errors | Status machines, EF queries |
| `Infrastructure/` | Host: middleware, logs, `/ready` | Domain rules |
| `Program.cs` | Composition root (DI, pipeline, endpoint map) | Use-case logic |

**Today vs target:** architecture.md’s CQRS / MediatR box is the *target*. Today, application **services** are the command/query handlers. Do not add MediatR, `ICaseRepository`, or a second read model unless that is an explicit decision.

GraphQL and temporary REST **share** Application models (`LoginRequest` / `LoginResponse`) so both adapters stay identical.

## Naming

| Kind | Pattern | Examples |
|---|---|---|
| Use-case class | `{Verb}{Noun}Service` | `CreateDraftCaseService`, `UploadDocumentService` |
| Shared query helper | descriptive, still in Application | `CaseVisibility` |
| Request / response | `{UseCase}Request` / `{UseCase}Response` or shared `CaseResponse` | `CreateDraftCaseRequest` |
| EF config | `{Entity}Configuration` | `CaseConfiguration` |
| Test class | `{Subject}Tests` | `CreateDraftCaseTests`, `TenantIsolationTests` |
| Test method | `Pascal_with_underscores` describing the outcome | `Customer_creates_draft_with_status_Draft_and_empty_FormData` |
| GraphQL field | camelCase in the schema (Hot Chocolate default from C# PascalCase) | `createDraftCase` |
| Postgres table | snake_case plural | `cases`, `users` |
| Enum stored in DB | C# PascalCase + `HasConversion<string>()` | `CaseStatus.Draft` → `"Draft"` (GraphQL may expose `DRAFT`) |

Role **names** must stay aligned: `UserRole` enum, JWT `role` claim, and `AuthRoles` constants.

## Application services

1. **One public method per use-case** (`CreateAsync`, `UpdateAsync`, `ListAsync`). Inject `AppDbContext` directly.
2. **Tenant and actor come from JWT** (`ICurrentTenant`, `ICurrentUser`), never from GraphQL/REST input (ADR-007).
3. **Return a result tuple**; do not throw for expected failures. GraphQL maps the tuple to `GraphQLException` codes.

Typical write-tuple (extend, do not invent a new shape):

```csharp
(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)
```

| Condition | Return |
|---|---|
| Bad input | `ValidationErrors` populated |
| Missing claims / stale JWT subject | `Unauthorized: true` |
| Missing, other tenant, or not owner | `ErrorCode: "NOT_FOUND"` (do **not** use `AUTH_NOT_AUTHORIZED` — that would confirm the row exists) |
| Illegal status transition | `ErrorCode: "DOMAIN"` |

4. Set `TenantId` / `CustomerUserId` on **insert** from the JWT. Query filters do not protect writes.
5. Trim user-facing strings. Empty FormData normalizes to `"{}"`.
6. Keep validation helpers `internal` next to the services that share them (`CaseDraftValidation`).

```csharp
// Prefer — owner mismatch is indistinguishable from missing
if (entity is null || entity.CustomerUserId != customerUserId.Value)
    return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);

// Avoid — teaches an attacker the case exists
if (entity.CustomerUserId != customerUserId.Value)
    return (…, "AUTH_NOT_AUTHORIZED", …);
```

## Tenancy and auth

- Every tenant-owned entity **must** implement `ITenantScoped`. `Tenant` itself does not.
- EF global filters fail closed: no JWT tenant → **zero** `ITenantScoped` rows.
- `AuditEntry` is append-only (KYC-050): write via `AuditRecorder` in the same transaction as the domain change; **no** update/delete application API. Read is Reviewer/TenantAdmin-only via `caseAuditEntries` (KYC-051).
- `IgnoreQueryFilters()` is only for login (and similar pre-auth lookups). Grep it before adding another call.
- GraphQL is deny-by-default: `[Authorize]` on `Query` / `Mutation`; `[AllowAnonymous]` only on `login` and `registerTenant`.
- Role gates on write fields: `[Authorize(Roles = new[] { AuthRoles.Customer })]`. List/detail stay “any authenticated role”; **visibility** is Application (`CaseVisibility`).
- Defense in depth: `[Authorize]` is not enough — the service still reads `ICurrentUser` / `ICurrentTenant`.
- HTTP `/graphql` is `.AllowAnonymous()` so login can reach Hot Chocolate. The **schema** is the real gate.

## GraphQL vs REST

| Surface | Use for |
|---|---|
| GraphQL `/graphql` | Public contract for identity, cases (incl. `customerEmail`), document **metadata**, case audit (ADR-002) |
| REST `POST /api/cases/{caseId}/documents` | Multipart upload (intentional; keep it REST) |
| REST `GET /api/cases/{caseId}/documents/{documentId}` | Authenticated download stream (intentional; keep it REST) |
| REST `POST /api/register-tenant`, `POST /api/login` | Temporary twins of GraphQL; same Application services; retire when UIs consume GraphQL (DoD) |

Keep `Query` / `Mutation` thin. New fields inherit type-level `[Authorize]` unless you add `[AllowAnonymous]` — do that only for a documented anonymous identity operation.

## Error codes

GraphQL returns HTTP 200 with `errors[].extensions.code`. Use these codes only:

| Code | Meaning |
|---|---|
| `VALIDATION` | Client input (title, FormData, skip/take, file type/size) |
| `AUTH_NOT_AUTHENTICATED` | No / invalid JWT on a protected field (Hot Chocolate) |
| `AUTH_NOT_AUTHORIZED` | Authenticated, wrong role |
| `AUTH_FAILED` | Generic login failure or stale JWT subject |
| `NOT_FOUND` | Missing **or not visible** |
| `DOMAIN` | Legal input, illegal state (submit a non-draft) |

REST document **upload and download** use `STORAGE` (HTTP 502) for MinIO/object-store failures — never map those to `VALIDATION`.

Local browser UIs are allowed via `Cors:AllowedOrigins` (`http://localhost:4200`, `http://localhost:5173`). Do not add `*` or reflect the request Origin. Security headers / HSTS stay W6.

Do not invent `FORBIDDEN`, `CONFLICT`, or leak emails / existence on login. Login failures stay generic (`LoginService.GenericAuthFailure`).

## Persistence

- Fluent API in `Data/Configurations` — one class per entity. No `[Table]` / `[MaxLength]` on Domain types.
- `DeleteBehavior.Restrict` on FKs unless a story says otherwise.
- Index tenant-scoped list paths (`TenantId + Status`, `TenantId + CustomerUserId`).
- Enums as strings (`HasConversion<string>()`), not integers.
- Provider-specific types stay in `AppDbContext` (e.g. `jsonb` only when `Database.IsNpgsql()` so SQLite tests still work).
- Migrations: `dotnet ef migrations add NameOfChange --output-dir Data/Migrations`. Review `Up()` / `Down()`. Do not hand-edit `*Designer.cs` or the model snapshot except via EF.
- `EnsureCreated` in SQLite tests is **not** a substitute for a real migration. Provider-specific schema is proven in `PostgresIntegrationTests`.

## Logging and secrets

- JSON stdout; include `RequestId` (`X-Request-Id`).
- Log method, path, status, duration — **never** bodies, query strings, headers, passwords, JWTs, FormData, connection strings, or MinIO keys.
- Auth / readiness failures: codes and exception **types**, not emails or tokens.
- Skip Information logs for `GET /health`.
- Prefer `[LoggerMessage]` source-generated logging (see `RequestLoggingMiddleware`).
- Committed `appsettings.json` is **shape only** (empty secrets). Local values: gitignored `appsettings.Development.json` or env / user-secrets. Never put secrets in `launchSettings.json`.

## Dependency injection

| Lifetime | Typical types |
|---|---|
| Scoped (per request) | `AppDbContext`, application services, `ICurrentTenant`, `ICurrentUser` |
| Singleton | `JwtTokenService`, `IPasswordHasher<User>`, health checks, `InMemoryObjectStorage` |
| Singleton vs scoped for ports | `IObjectStorage`: InMemory / MinIO are singletons today — do not capture `AppDbContext` inside them |

Register new services in `Program.cs` next to their neighbors. Fail fast at startup if required config is missing (Postgres, JWT signing key ≥ 32 chars).

## Tests

- Live in `Kyc.Api.Tests`, never beside production files (test packages must not ship).
- Default host: `ApiFactory` + SQLite in-memory + `ObjectStorage:Provider=InMemory`.
- Real migrations / `jsonb`: `PostgresApiFactory` + `[PostgresFact]` (skips unless `KYC_TEST_POSTGRES` is set).
- Prefer HTTP/GraphQL through `WebApplicationFactory<Program>` for behavior. Use `FakeCurrentTenant` + raw `AppDbContext` only for isolation proofs that should not go through GraphQL.
- Assert GraphQL `errors[].extensions.code`, not only HTTP status.
- New tenant-owned entity: add or extend a tenant-isolation test (tenant A must not read tenant B).
- New mutation: role test (wrong role → `AUTH_NOT_AUTHORIZED`) and visibility test (non-owner / other tenant → `NOT_FOUND`).
- `dotnet test` on a laptop without Compose must stay green (Postgres facts skip).

## Checklist for a new API story

1. Domain type in the right bounded-context folder; `ITenantScoped` if it has a tenant.
2. Fluent configuration + migration (if schema changes).
3. Application service: JWT ids, tuple errors, `CancellationToken`.
4. Thin GraphQL field (or dedicated REST only if the payload is a file).
5. Tests: happy path, `VALIDATION`, `DOMAIN` / `NOT_FOUND`, role gate, isolation if new entity.
6. Update [apps/api/README.md](../apps/api/README.md) operation index / done-checks — not this file — when the contract grows.
