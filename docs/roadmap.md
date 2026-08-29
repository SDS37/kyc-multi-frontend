| Week | Dates (example) | Goal | Stories | Checkpoint |
|------|-----------------|------|---------|------------|
| **W1** | Week 1 | Foundation + Identity | KYC-001 to KYC-014 (incl. KYC-004 API scaffold before KYC-010) | API runs, register/login works, tenant in JWT |
| **W2** | Week 2 | GraphQL + Cases backend | KYC-020 to KYC-037 | Case lifecycle works via GraphQL playground |
| **W3** | Week 3 | Documents + Audit | KYC-040 to KYC-051 | Upload/download + audit entries exist |
| **W4** | Week 4 | Angular Admin | KYC-060 to KYC-064 | Reviewer can finish a case in Angular |
| **W5** | Week 5 | React Customer | KYC-070 to KYC-074 | Customer happy path works in React |
| **W6** | Week 6 | Vue Reports + Security + Seed | KYC-080, KYC-081, KYC-090 to KYC-101, KYC-093 (rate limits when leaving localhost), KYC-103 / KYC-104 (readiness + observability backlog) | All 3 UIs usable, isolation tests green |
| **W7** | Buffer | Federation attempt, polish, docs | leftover + Module Federation spike | Public README and architecture complete |

Hardening outside the weekly product slices: **KYC-102** (`api-ci` workflow + `global.json` SDK pin) landed after W1. **KYC-103** (readiness / EF retries / timeouts), **KYC-104** (structured logs / request id), **KYC-105** (GraphQL introspection/depth, EF command log level, MinIO pin), **KYC-106** (case NOT_FOUND / FormData caps / atomic status), **KYC-107** (login dummy verify / registerTenant retries), **KYC-108** (api-ci SHA pins + Postgres test slice), and **KYC-109** (login password cap, updateDraftCase DOMAIN-before-FormData, status docs) are on the API.

Backlog until the API leaves localhost (do not treat this as “only rate limits”):

- **KYC-093** — rate-limit `registerTenant` and `login`
- **KYC-091** (W6) — CORS allow-list and security headers (when the first UI exists)
- TLS / HTTPS redirect and HSTS on any non-local deploy
- Production log levels (EF SQL already Warning in committed `appsettings.json`; keep host noise down)
- Bind or authenticate `GET /ready` (liveness `/health` can stay anonymous)
- Abuse controls on public `registerTenant` beyond login throttling (KYC-093)
- GraphQL cost analyzer when list/document fields land (KYC-036+ / W3); depth limit is KYC-105

### Weekly checkpoint questions

At the end of every week, answer:

1. What is demoable now?
2. Which MVP stories slipped?
3. What is cut vs moved to buffer?
4. Is tenant isolation still proven?

### Time-control rules

- If Module Federation is not working by **end of W5**, keep 3 separate apps. Do not block MVP.
- If Vue is late, keep Reports as a **single read-only page**.
- If document storage is slow, use local disk in dev and keep the same interface.
- Do not add notifications, billing, OCR, or custom workflows.