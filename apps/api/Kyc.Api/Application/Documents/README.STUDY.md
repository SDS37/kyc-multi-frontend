# Study: `Application/Documents`

Study tour of this folder. Distinct from the official README. Parent layer: [../README.STUDY.md](../README.STUDY.md).

**Aligned with:** `feat/kyc-042-download-document` / KYC-042.

## Purpose

Document **upload**, **list**, and **download** for KYC cases. GraphQL carries metadata only (`documents(caseId)` / case detail). REST carries bytes: multipart upload + authenticated download stream (ADR-002). MinIO bucket stays private — clients never get storage keys or direct object URLs.

## Why these files exist

| File | Role |
|---|---|
| `IObjectStorage` | Port: put / open-read / delete by opaque key. Application never imports AWSSDK types. |
| `MinioObjectStorage` | Adapter: S3-compatible MinIO via AWSSDK.S3. Ensures bucket once (no public policy). |
| `InMemoryObjectStorage` | Tests / blank Provider / design-time (`dotnet ef`). |
| `ObjectStorageOptions` | Config section `ObjectStorage`. Blank or `InMemory` → in-process; `Minio` needs endpoint + keys. |
| `DocumentUploadValidation` | Filename sanitize, content-type allow-list, magic bytes, 10 MB, storage key shape. |
| `UploadDocumentService` | Customer-only; Draft\|Submitted; owner only → `NOT_FOUND`; put then DB insert; compensate delete on DB fail. |
| `ListDocumentsService` | KYC-041: same visibility as `case` / `cases`; metadata only; shared `LoadMetadataAsync` for detail. |
| `DownloadDocumentService` | KYC-042: same visibility as list; streams bytes via REST; never returns keys/URLs. |

## Design choice (KYC-042)

**Authenticated API stream**, not presigned MinIO URLs, for MVP:

- Auth checked on every download (same `CaseVisibility` as list).
- Works with `InMemoryObjectStorage` in tests without fake endpoints/CORS.
- Satisfies AC: time-limited URL *or* stream; no public bucket access.
- Presigned URLs can wait until browser clients need offloaded bandwidth.

## Angular / Java analog

| You already know | Here |
|---|---|
| `HttpClient` `FormData` POST | Client → `POST /api/cases/{id}/documents` field **`file`** only |
| `HttpClient.get(..., { responseType: 'blob' })` | Client → `GET /api/cases/{caseId}/documents/{documentId}` |
| NestJS `FileInterceptor` + S3 SDK | Minimal API endpoint + `IObjectStorage` |
| “Never return the S3 key to the UI” | `StorageKey` on entity; GraphQL/REST metadata omit it; download streams through API |

## Flow (say this in reviews)

```mermaid
sequenceDiagram
    participant UI as Authorized client
    participant REST as Program MapGet
    participant App as DownloadDocumentService
    participant S3 as IObjectStorage
    participant EF as AppDbContext

    UI->>REST: GET + JWT (Customer/Reviewer/TenantAdmin)
    REST->>App: DownloadAsync(caseId, documentId)
    App->>App: CaseVisibility (same as list)
    App->>EF: Document by id + caseId
    App->>S3: OpenReadAsync(storageKey)
    App-->>UI: file stream (Content-Disposition filename; no storage key)
```

**Key shape:** `tenants/{tenantId:N}/cases/{caseId:N}/{documentId:N}/{safeFileName}` — opaque to clients.

## Error codes (REST JSON)

### Upload

| Situation | HTTP | `code` |
|---|---|---|
| Bad multipart / type / size / magic | 400 | `VALIDATION` |
| Missing JWT identity | 401 | `AUTH_FAILED` |
| Non-Customer role | 403 | `AUTH_NOT_AUTHORIZED` |
| Not owner / missing case | 404 | `NOT_FOUND` |
| Wrong status (e.g. Approved) | 422 | `DOMAIN` |

### Download

| Situation | HTTP | `code` |
|---|---|---|
| Empty ids | 400 | `VALIDATION` |
| Missing JWT / unknown role | 401 | `AUTH_FAILED` / framework 401 |
| Outside visibility / missing doc / missing blob | 404 | `NOT_FOUND` |

## What to skip

- MinIO console clicking except when verifying a put/get after local smoke.
- Presigned URL minting — not in MVP download story.

## Links

- [Domain Document](../../Domain/README.STUDY.md)
- [Cases detail documents](../Cases/README.STUDY.md)
- [infrastructure MinIO](../../../../../infrastructure/README.STUDY.md)
- [AWS SDK for .NET S3](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/s3-apis-intro.html)
- ADR-001 / ADR-006 in [architecture-decision-records](../../../../../docs/architecture-decision-records.md)
