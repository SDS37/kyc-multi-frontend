# After MVP — production-shaped wishlist

How this demo starts to look like a **whole, functional production product** — without pretending those pieces are in flight.

**Not this file:** week-by-week MVP ([roadmap.md](roadmap.md) W1–W7), locked decisions ([architecture-decision-records.md](architecture-decision-records.md)), or “do it because the diagram draws it.” If this file and an ADR disagree, the ADR wins.

**Rule for agents and future-you:** this is a **wishlist with triggers**. Do not open a PR from a row here unless the trigger is true *or* a new GitHub story exists. A Compose Redis container is not a trigger. Live diagrams: [architecture.md](architecture.md) (dotted Redis = unused).

## Where MVP stops

[DoD.md](DoD.md) is already true for the product slice: Customer (React), Reviewer (Angular), reports overview (Vue), isolation tests, local README.

Still on the **MVP roadmap** (finish these first; they are not “beyond”):

| Item | Where |
|---|---|
| Demo seed (users per role, cases, files) | [KYC-101](https://github.com/SDS37/kyc-multi-frontend/issues/42) |
| Colleague runbook polish | [KYC-100](https://github.com/SDS37/kyc-multi-frontend/issues/41) |
| CSP / HTTPS redirect outside Development | [issue #108](https://github.com/SDS37/kyc-multi-frontend/issues/108) |
| Angular Playwright smoke | [KYC-110](https://github.com/SDS37/kyc-multi-frontend/issues/98) |
| Module Federation **spike** (keep 3 apps if it fails) | W7 / [ADR-005](architecture-decision-records.md) |

Localhost hardening that already landed (rate limits, headers, captcha, invites) stays as-is until you leave a single-process API.

## Wishlist (after DoD)

Each row is “the product would feel complete if…” plus **when** to actually build it.

### 1. People can work without SQL

| Gap today | Production-shaped | Trigger |
|---|---|---|
| `registerTenant` creates one TenantAdmin. Reviewer/Customer come from seed or the database. No users screen. | Invite / list / deactivate users (Customer, Reviewer) behind TenantAdmin. | You cannot onboard a colleague without seed or SQL. **API first**, then one UI (probably Angular). Do not invent a user table in a frontend. |
| Seed ([KYC-101](https://github.com/SDS37/kyc-multi-frontend/issues/42)) is the demo stand-in | Keep seed for local/demo even after invites exist | Always |

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
| Upload is put-then-DB; compensating delete can fail quietly; post-put DB errors can still look like validation | Map that path to `STORAGE`; log failed deletes at Error | Customer upload retries confuse people, or orphans pile up in MinIO |
| `updateDraftCase` has no status CAS (submit + update can race) | `WHERE Status == Draft` like submit | React customers edit and submit at once (already a W5 risk) |
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
| Local HTTP; CSP/HTTPS follow-up still open | TLS, redirect, HSTS, CSP on a real host | Leaving localhost ([#108](https://github.com/SDS37/kyc-multi-frontend/issues/108) first) |
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

1. Finish W6 seed + runbook + [#108](https://github.com/SDS37/kyc-multi-frontend/issues/108) so a colleague can click through all three roles with files.
2. `updateDraftCase` CAS + honest upload error mapping (small API; protects React).
3. Identity invites (API) if humans must join without SQL.
4. TLS / CSP on a real deploy; `/ready` not public.
5. Redis **only** with a second API instance or token revoke.
6. W7 MF spike; keep three apps if it is not boringly stable.
7. Refresh tokens, audit UI, extra Vue pages, i18n — when a demo or operator actually misses them.

That sequence is the difference between a **wishlist** and a second fake MVP.
