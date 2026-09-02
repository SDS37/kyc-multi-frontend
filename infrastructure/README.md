# Infrastructure

Local dependencies for the KYC platform: PostgreSQL, Redis, and MinIO.

**The API uses Postgres and MinIO.** Redis is started for local DX and has **no client** — [beyond-mvp.md](../docs/beyond-mvp.md) §4.

## Run

```bash
cp infrastructure/.env.example infrastructure/.env
docker compose -f infrastructure/docker-compose.yml up -d
```

Compose loads `infrastructure/.env` automatically. Defaults are local-only; change them in `.env`.

| Service    | Host port              | Credentials (defaults)   |
|------------|------------------------|--------------------------|
| PostgreSQL | `127.0.0.1:5432`       | `kyc` / `changeme` / db `kyc_db` |
| Redis      | `127.0.0.1:6379`       | password `changeme` (unused by API) |
| MinIO API  | `127.0.0.1:9000`       | `minio` / `changeme1`    |
| MinIO UI   | `127.0.0.1:9001`       | same as API              |

Images: Postgres `postgres:18-alpine`, Redis `redis:8-alpine`, MinIO `minio/minio:RELEASE.2025-09-07T16-13-09Z` (Hub no longer updates `:latest` for community).

Data is stored in named volumes: `postgres_data`, `redis_data`, `minio_data`.

The API and frontends are **not** Compose services yet. Colleague copy-paste: [root README runbook](../README.md#colleague-runbook). API: [apps/api/README.md](../apps/api/README.md). Angular (`localhost:4200`), React (`localhost:5173`), and Vue (`localhost:5174`) on the host. Today vs after MVP: [docs/architecture.md](../docs/architecture.md), [docs/beyond-mvp.md](../docs/beyond-mvp.md).
