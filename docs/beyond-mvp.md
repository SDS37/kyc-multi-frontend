# After MVP — production-shaped wishlist

How this demo starts to look like a **whole, functional production product** — without pretending those pieces are in flight.

**Not this file:** week-by-week MVP ([roadmap.md](roadmap.md) W1–W7), locked decisions ([architecture-decision-records.md](architecture-decision-records.md)), or “do it because the diagram draws it.” If this file and an ADR disagree, the ADR wins.

**Rule for agents and future-you:** this is a **wishlist with triggers**. Do not open a PR from a row here unless the trigger is true *or* a new GitHub story exists. A Compose Redis container is not a trigger. Live diagrams: [architecture.md](architecture.md) (dotted Redis = unused).

## Where MVP stops

[DoD.md](DoD.md) is already true for the product slice: Customer (React), Reviewer (Angular), reports overview (Vue), isolation tests, local README.

W6 leftover that was still listed here (KYC-100, KYC-095, KYC-110) is **done**. [KYC-111](https://github.com/SDS37/kyc-multi-frontend/issues/120) (submit FormData at persist), [KYC-112](https://github.com/SDS37/kyc-multi-frontend/issues/121) (React/Vue production API URL guard), [KYC-113](https://github.com/SDS37/kyc-multi-frontend/issues/122) (upload post-put `STORAGE` 502), [KYC-114](https://github.com/SDS37/kyc-multi-frontend/issues/123) (docs consistency), and [KYC-115](https://github.com/SDS37/kyc-multi-frontend/issues/124) (Angular transport errors) are **done**. `updateDraftCase` already compare-and-swaps `Status == Draft` (KYC-095); that is not an open gap. Still on the **MVP roadmap** (W7 — not “beyond”):

| Item | Where |
|---|---|
| Module Federation **spike** (keep 3 apps if it fails; **not** a polish gate) | [KYC-120](https://github.com/SDS37/kyc-multi-frontend/issues/125) / [ADR-005](architecture-decision-records.md) |

Localhost hardening that already landed (rate limits, headers, captcha, `registerTenant` invite codes) stays as-is until you leave a single-process API. Those codes gate **new tenants** (KYC-093). They are not TenantAdmin user invite/list (Customer/Reviewer) — that is §1 below.

## Wishlist (after DoD)

Each row is “the product would feel complete if…” plus **when** to actually build it.

### 1. People can work without SQL

| Gap today | Production-shaped | Trigger |
|---|---|---|
| `registerTenant` creates one TenantAdmin. Reviewer/Customer come from seed or the database. No users screen. | Invite / list / deactivate users (Customer, Reviewer) behind TenantAdmin. | You cannot onboard a colleague without seed or SQL. **API first**, then one UI (probably Angular). Do not invent a user table in a frontend. |
| Seed ([KYC-101](https://github.com/SDS37/kyc-multi-frontend/issues/42)) is the demo stand-in | Keep seed for local/demo even after user invite/list exists | Always |

### 2. One URL, one login session (optional)

| Gap today | Production-shaped | Trigger |
|---|---|---|
| Three apps, three origins (`:4200`, `:5173`, `:5174`) | A shell that loads remotes (Module Federation) **or** a reverse-proxied same-site deploy | W7 spike is stable **and** a reviewer should not juggle three tabs. If the spike fails, keep three apps (ADR-005). |
| JWT 60 minutes, no refresh, no logout kill | Refresh tokens; optional revoke list | Sessions are too short, or “Sign out” must invalidate the token on the server |

Redis belongs here only for **shared revoke / rate-limit state** across API instances — see §4.

### 3. Files and cases feel like an ops-backed product

| Gap today | Production-shaped | Trigger |
|---|---|---|
| `/ready` is Postgres only; MinIO can be down while the API looks ready | Separate storage probe or `/ready` tag; UIs already treat `STORAGE` 502 | You deploy and need orchestrators to stop traffic when object storage is dead |
| API is not a Compose service | `docker compose up` includes the API (and optionally the UIs) | Someone else must demo without `dotnet run` + three `npm start`s |
| Audit history is API-only | Reviewer can open `caseAuditEntries` in Angular | Compliance demo needs a visible trail, not GraphQL playground |

### 4. Redis (the unused container)

Compose Redis is **local DX**, not a feature ([ADR-006](architecture-decision-records.md)). KYC-093 rate limits are **in-memory** on one process; Redis-backed limiters were out of scope.

| Use | Production-shaped | Trigger |
|---|---|---|
| Auth 429 counters | Shared limiter so two API replicas cannot be doubled | You run **more than one** API instance |
| JWT deny list | Store `jti` (or user id) until expiry | You must kill tokens before 60 minutes |
| Cache | Case list / Vue counts | Only after a measured Postgres hotspot — not “because Redis is up” |

Do not cache KYC documents or case rows “in Redis.” Bytes stay MinIO; source of truth stays Postgres.

### 5. Hardening that looks like production

| Gap today | Production-shaped | Trigger |
|---|---|---|
| Local HTTP (certs still yours) | TLS on a real host; API already redirects + HSTS + CSP outside Development | Leaving localhost with a real certificate |
| Anonymous `/ready` | Bind to loopback / mesh, or a probe token | The API is on a public network |
| Logs + `/ready` only (KYC-104) | Request traces / APM if you operate it | You cannot debug a failed review in production |
| GraphQL depth limit only | Cost analyzer when lists/documents grow | Playground or a client can still be expensive |
| English `*.messages.ts` catalogs | Second locale + switcher | Product requires Swedish (or similar) — all three apps, same keys, per-app loaders |
| Vue is one overview page | More reports only if a stakeholder asks | Do not grow Vue for symmetry |

### 6. What still does **not** belong

These keep the portfolio honest. They do not make KYC “more production.”

- MediatR / domain-events rewrite to match old diagrams
- Module Federation from week 1, or a host if the W7 spike is unstable
- Shared React/Angular/Vue widget library (tokens + GraphQL stay the share boundary)
- Notifications, billing, OCR, custom workflows ([roadmap](roadmap.md) time-control)
- Sheriff / Nx / tsarch for a three-feature admin
- Redis as decoration

## Suggested order (if you ever execute this)

1. **W7 polish** is done (KYC-111–115) — not Redis, not an MF host.
2. User invite/list API if humans must join without SQL.
3. TLS on a real deploy; `/ready` not public.
4. Redis **only** with a second API instance or token revoke.
5. **W7 spike** [KYC-120](https://github.com/SDS37/kyc-multi-frontend/issues/125) in parallel with polish; keep three apps if it is not boringly stable. A one-URL **host** is §2 after the spike.
6. Refresh tokens, audit UI, extra Vue pages, i18n — when a demo or operator actually misses them.

That sequence is the difference between a **wishlist** and a second fake MVP.
