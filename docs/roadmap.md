| Week | Status | Goal | Stories | Checkpoint |
|------|--------|------|---------|------------|
| **W1** | Done | Foundation + Identity | KYC-001 to KYC-014 (incl. KYC-004 API scaffold before KYC-010) | API runs, register/login works, tenant in JWT |
| **W2** | Done | GraphQL + Cases backend | KYC-020 to KYC-037 | Case lifecycle works via GraphQL playground |
| **W3** | Done | Documents + Audit | KYC-040 to KYC-051 | Upload/download + audit entries exist |
| **W4** | Done | Angular Admin | KYC-060 to KYC-065; **KYC-091 CORS** (local origins — prerequisite) | Reviewer can finish a case in Angular |
| **W5** | Done | React Customer | KYC-070 to KYC-074 | Customer happy path works in React |
| **W6** | Next | Vue Reports + Security + Seed | KYC-080, KYC-081, KYC-090 to KYC-101, KYC-093 (rate limits when leaving localhost), KYC-091 remainder (security headers / HSTS) | All 3 UIs usable, isolation tests green |
| **W7** | Planned | Federation attempt, polish, docs | leftover + Module Federation spike | Public README and architecture complete |

**W5 demoable now:** Customer signs in (React), creates a draft, fills FormData, uploads PDF/PNG/JPG (≤10 MB), and submits. Reviewer finishes the case in Angular (W4). Vue reports overview is KYC-081 (counts + latest 10). Remaining W6 is seed + security hardening.

Hardening outside the weekly product slices: **KYC-102** (`api-ci` workflow + `global.json` SDK pin) landed after W1. **KYC-103** (readiness / EF retries / timeouts), **KYC-104** (structured logs / request id), **KYC-105** (GraphQL introspection/depth, EF command log level, MinIO pin), **KYC-106** (case NOT_FOUND / FormData caps / atomic status), **KYC-107** (login dummy verify / registerTenant retries), **KYC-108** (api-ci SHA pins + Postgres test slice), and **KYC-109** (login password cap, updateDraftCase DOMAIN-before-FormData, status docs) are on the API.

Backlog until the API leaves localhost (do not treat this as “only rate limits”):

- **KYC-093** — rate-limit `registerTenant` and `login`
- **KYC-091** — CORS allow-list for local UIs is on the API (W4 prerequisite: `http://localhost:4200`, `http://localhost:5173`). Security headers / HSTS stay W6.
- TLS / HTTPS redirect and HSTS on any non-local deploy
- Production log levels (EF SQL already Warning in committed `appsettings.json`; keep host noise down)
- Bind or authenticate `GET /ready` (liveness `/health` can stay anonymous)
- Abuse controls on public `registerTenant` beyond login throttling (KYC-093)
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
