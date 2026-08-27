# Infrastructure

Local dependencies for the KYC platform: PostgreSQL, Redis, and MinIO.

## Run

```bash
cp infrastructure/.env.example infrastructure/.env
docker compose -f infrastructure/docker-compose.yml up -d
```

Compose loads `infrastructure/.env` automatically. Defaults are local-only; change them in `.env`.

| Service    | Host port              | Credentials (defaults)   |
|------------|------------------------|--------------------------|
| PostgreSQL | `127.0.0.1:5432`       | `kyc` / `changeme` / db `kyc_db` |
| Redis      | `127.0.0.1:6379`       | password `changeme`      |
| MinIO API  | `127.0.0.1:9000`       | `minio` / `changeme1`    |
| MinIO UI   | `127.0.0.1:9001`       | same as API              |

Data is stored in named volumes: `postgres_data`, `redis_data`, `minio_data`.

The API and frontends are **not** Compose services yet. Run the API on the host with `dotnet run` — see [apps/api/README.md](../apps/api/README.md). Frontends are still placeholders (W4–W6). Target architecture: [docs/architecture.md](../docs/architecture.md).
