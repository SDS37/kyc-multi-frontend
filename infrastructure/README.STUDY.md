# Study: `infrastructure`

Study tour of this folder. Distinct from the official README. Runbook: [README.md](README.md).

**Aligned with:** `main` after W5 (Compose deps; Angular + React UIs on the host).

## Purpose

This folder is **local production-shaped dependencies**, not the KYC application. Compose starts three containers. The .NET API and the UIs (Angular `:4200`, React `:5173`, Vue `:5174`) run **on your machine** and connect in over localhost.

If you only remember one sentence: **from the API, Postgres is `127.0.0.1:5432`, not hostname `postgres`.** `postgres` is the DNS name *inside* the Compose network. Mixing those up is the usual “connection refused” after a working Docker Desktop install. Same idea for MinIO: API uses `http://127.0.0.1:9000`, not hostname `minio`.

## Why these files exist

| File | Why |
|---|---|
| `docker-compose.yml` | Declares Postgres, Redis, MinIO, named volumes, healthchecks, **localhost-bound ports**. |
| `.env.example` | Committed credential *shape*. Copy to gitignored `.env`. |
| `.env` | Local secrets. Never commit. |

There is no `Dockerfile` for the API yet (comment in compose). Containerizing the API is a later ops step.

## Why three services

ADR-006: Postgres for relational data, MinIO for KYC files, Redis optional later ([beyond-mvp.md](../docs/beyond-mvp.md)). **Today the API uses Postgres + MinIO**. Redis is unused — do not claim “we use Redis” in a review.

```mermaid
flowchart LR
    subgraph host ["Your laptop"]
        API["dotnet run :5295"]
        Ang["Angular :4200"]
        React["React :5173"]
        Vue["Vue :5174"]
    end
    subgraph compose ["Docker Compose"]
        PG[(Postgres :5432)]
        RD["Redis :6379 (unused)"]
        MN[(MinIO :9000 / console :9001)]
    end
    API -->|"cases / users / document metadata"| PG
    API -->|"document bytes KYC-040"| MN
    API -.->|"no client"| RD
    Ang --> API
    React --> API
    Vue --> API
```

## Angular analog

This is not `ng serve`’s proxy. It is the backend you would otherwise mock. Binding ports to `127.0.0.1` (not `0.0.0.0`) means other machines on the Wi-Fi cannot hit your DB — a small security habit for a portfolio laptop.

Volumes (`postgres_data`, …) survive `compose down` without `-v`. Data persists; `down -v` is the “wipe the database” hammer.

## What to notice in compose (without memorizing YAML)

- **Postgres 18** alpine; `PGDATA` parent mount (`/var/lib/postgresql`) because PG18 changed the data dir — the comment in the file is worth reading once.
- **Healthcheck** `pg_isready` — Compose `healthy` is what you wait for before `dotnet ef database update`.
- **MinIO tag is pinned** to a `RELEASE.*` digest-era tag (Hub froze `:latest` for community). KYC-105. Do not “fix” it to `:latest`.
- **Redis requirepass** — even locally a password so we do not get used to open Redis.

## Today vs target

| Dependency | Today |
|---|---|
| Postgres | EF Core + migrations (including `documents`) |
| MinIO | `ObjectStorage:Provider=Minio` in Dev settings; S3-compatible put/delete |
| Redis | Compose only — no client ([beyond-mvp.md](../docs/beyond-mvp.md) §4) |

Blank / missing `ObjectStorage:Provider` **fails closed at startup**. Use `Minio` (local Dev example) or `InMemory` only in Development/Testing. Copy `appsettings.Development.json.example`.

## What to skip

- Treating MinIO console as the product UI — metadata is on GraphQL `case.documents`; bytes are opaque keys.
- Installing native Postgres “to be simpler” — the project standard is Compose ([guide](../docs/guides/dotnet-api-for-frontend-engineers.md)).

## Links

- [README.md](README.md) — ports and default credentials
- [Docker Compose](https://docs.docker.com/compose/)
- [Compose file reference](https://docs.docker.com/reference/compose-file/)
- [Postgres Docker](https://hub.docker.com/_/postgres)
- [MinIO](https://min.io/docs/minio/linux/index.html)
- [Redis](https://redis.io/docs/latest/)
- [ADR-006](../docs/architecture-decision-records.md)
- [beyond-mvp.md](../docs/beyond-mvp.md) — when Redis or API-in-Compose would be justified
- [bind to localhost](https://docs.docker.com/engine/network/#published-ports) (published ports)
