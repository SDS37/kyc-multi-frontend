| Week | Status | Goal | Stories | Checkpoint |
|------|--------|------|---------|------------|
| **W1** | Done | Foundation + Identity | KYC-001 to KYC-014 (incl. KYC-004 API scaffold before KYC-010) | API runs, register/login works, tenant in JWT |
| **W2** | Done | GraphQL + Cases backend | KYC-020 to KYC-037 | Case lifecycle works via GraphQL playground |
| **W3** | Done | Documents + Audit | KYC-040 to KYC-051 | Upload/download + audit entries exist |
| **W4** | Done | Angular Admin | KYC-060 to KYC-065; **KYC-091 CORS** (local origins — prerequisite) | Reviewer can finish a case in Angular |
| **W5** | Done | React Customer | KYC-070 to KYC-074 | Customer happy path works in React |
| **W6** | Next | Security + Seed | KYC-095, KYC-100, KYC-101, KYC-110, [issue #108](https://github.com/SDS37/kyc-multi-frontend/issues/108) (CSP / HTTPS redirect — **done**) | Isolation tests green; demo seed; leave-localhost hardening |
| **W7** | Planned | Federation attempt, polish, docs | leftover + Module Federation spike | Public README and architecture complete |

**W5+ demoable now:** Customer signs in (React), creates a draft, fills FormData, uploads PDF/PNG/JPG (≤10 MB), and submits. Reviewer finishes the case in Angular (W4). Vue reports overview is KYC-081 (counts + latest 10). Remaining W6 is KYC-095 (review punch-list) and Playwright smoke.

Hardening outside the weekly product slices: **KYC-102** (`api-ci` workflow + `global.json` SDK pin) landed after W1. **KYC-103** (readiness / EF retries / timeouts), **KYC-104** (structured logs / request id), **KYC-105** (GraphQL introspection/depth, EF command log level, MinIO pin), **KYC-106** (case NOT_FOUND / FormData caps / atomic status), **KYC-107** (login dummy verify / registerTenant retries), **KYC-108** (api-ci SHA pins + Postgres test slice), **KYC-109** (login password cap, updateDraftCase DOMAIN-before-FormData, status docs), **KYC-090** (FluentValidation on request DTOs; validation errors stay `VALIDATION`, not HTTP 500), **KYC-092** (GraphQL register→approve happy path + cross-tenant case isolation), and **KYC-093** (auth abuse controls) are on the API. **KYC-094** (login 429 + optional captcha in the three UIs) is done.

Backlog until the API leaves localhost (do not treat this as “only rate limits”):

- **KYC-093** — **Done.** Layered public-auth abuse controls: env-specific IP rate limits (login / register / other GraphQL), HTTP 429, in-memory account lockout, CAPTCHA on register outside Development, invite-only `registerTenant` when public registration is off.
- **KYC-094** — **Done.** Angular / React / Vue login: HTTP 429 → dedicated catalog copy (not a network failure; session stays); optional `captchaToken` on GraphQL `login` when `Captcha:RequiredForLogin` (Turnstile widget or test-provider field). Development unchanged.
- **KYC-100** — **Done.** Colleague runbook (Docker, API, three UIs, demo accounts).
- **KYC-101** — **Done.** Development demo seed: two tenants (`acme`, `globex`), users for each role, cases in every status, PNG on non-draft cases. Idempotent; `Seed:Enabled` (default true in Development).
- **KYC-095** — **Open.** Post-094 review punch-list: atomic `updateDraftCase` / upload status, GraphQL login/register op limits (aliases/batches), captcha `test` blocked outside Testing, in-process lockout atomicity, Angular prod API URL, React unmount + guest JWT + skip links. Not Redis, not [#108](https://github.com/SDS37/kyc-multi-frontend/issues/108).
- **KYC-091** — **Done.** CORS allow-list for local UIs (`http://localhost:4200`, `http://localhost:5173`, `http://localhost:5174`). Basic headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`) and non-Dev HSTS. No secrets in git. Closed as delivered.
- **[#108](https://github.com/SDS37/kyc-multi-frontend/issues/108)** — **Done.** Follow-up from KYC-091 (not a KYC-108 story): CSP on the API, HTTPS redirect **outside Development** only, Vite preview origins `4173` / `4174` (React / Vue `vite preview`).
- TLS certificates on a real host (HSTS + redirect already on outside Development)
- Production log levels (EF SQL already Warning in committed `appsettings.json`; keep host noise down)
- Bind or authenticate `GET /ready` (liveness `/health` can stay anonymous)
- GraphQL cost analyzer when document/list volume grows (post KYC-036 / W3); depth limit is KYC-105

### Weekly checkpoint questions

At the end of every week, answer:

1. What is demoable now?
2. Which MVP stories slipped?
3. What is cut vs moved to buffer?
4. Is tenant isolation still proven?

### Time-control rules

- MVP ships **3 separate apps** (ADR-005). W7 Module Federation is a spike only — if it is not stable by **end of W7**, keep separate apps. Do not treat MF as MVP scope.
- If Vue is late, keep Reports as a **single read-only page**.
- If document storage is slow, use local disk in dev and keep the same interface.
- Do not add notifications, billing, OCR, or custom workflows.

After Definition of Done, production-shaped follow-ups (Redis, user invite/list, MF host, TLS, …) live in [beyond-mvp.md](beyond-mvp.md). That file is a **triggered wishlist**, not W6/W7 work.
