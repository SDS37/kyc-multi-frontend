# Study: `Application/Documents`

Study tour of this folder. Distinct from the official README. Parent layer: [../README.STUDY.md](../README.STUDY.md).

**Aligned with:** `main` after KYC-041.

## Purpose

Document **upload** and **list** for KYC cases: validate bytes, put object storage, save metadata; list metadata by case. Download is KYC-042 (not here). GraphQL does **not** carry multipart; REST does upload (ADR-001 / ADR-002). List is GraphQL `documents(caseId)`.

## Why these files exist

| File | Role |
|---|---|
| `IObjectStorage` | Port: put / delete by opaque key. Application never imports AWSSDK types. |
| `MinioObjectStorage` | Adapter: S3-compatible MinIO via AWSSDK.S3. Ensures bucket once. |
| `InMemoryObjectStorage` | Tests / blank Provider / design-time (`dotnet ef`). |
| `ObjectStorageOptions` | Config section `ObjectStorage`. Blank or `InMemory` → in-process; `Minio` needs endpoint + keys. |
| `DocumentUploadValidation` | Filename sanitize, content-type allow-list, magic bytes, 10 MB, storage key shape. |
| `UploadDocumentService` | Customer-only; Draft\|Submitted; owner only → `NOT_FOUND`; put then DB insert; compensate delete on DB fail. |
| `ListDocumentsService` | KYC-041: same visibility as `case` / `cases`; metadata only; shared `LoadMetadataAsync` for detail. |

## Angular / Java analog

| You already know | Here |
|---|---|
| `HttpClient` `FormData` POST | Client → `POST /api/cases/{id}/documents` field **`file`** only |
| NestJS `FileInterceptor` + S3 SDK | Minimal API endpoint + `IObjectStorage` |
| “Never return the S3 key to the UI” | `StorageKey` on entity; GraphQL/REST metadata omit it |

## Flow (say this in reviews)

```mermaid
sequenceDiagram
    participant UI as Customer client
    participant REST as Program MapPost
    participant App as UploadDocumentService
    participant S3 as IObjectStorage
    participant EF as AppDbContext

    UI->>REST: multipart file + JWT Customer
    REST->>App: UploadAsync(caseId, file)
    App->>App: Validate type/size/magic; owner + Draft|Submitted
    App->>S3: PutAsync(storageKey, stream)
    App->>EF: Insert Document + SaveChanges
    alt DB fails
        App->>S3: DeleteAsync(storageKey)
    end
    App-->>UI: metadata (id, fileName, contentType, sizeBytes, uploadedAt, uploadedBy)
```

**Key shape:** `tenants/{tenantId:N}/cases/{caseId:N}/{documentId:N}/{safeFileName}` — opaque to clients.

## Error codes (REST JSON)

| Situation | HTTP | `code` |
|---|---|---|
| Bad multipart / type / size / magic | 400 | `VALIDATION` |
| Missing JWT identity | 401 | `AUTH_FAILED` |
| Non-Customer role | 403 | `AUTH_NOT_AUTHORIZED` |
| Not owner / missing case | 404 | `NOT_FOUND` |
| Wrong status (e.g. Approved) | 422 | `DOMAIN` |

## What to skip

- MinIO console clicking except when verifying a put after smoke upload.
- Implementing download here — KYC-042.

## Links

- [Domain Document](../../Domain/README.STUDY.md)
- [Cases detail documents](../Cases/README.STUDY.md)
- [infrastructure MinIO](../../../../../infrastructure/README.STUDY.md)
- [AWS SDK for .NET S3](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/s3-apis-intro.html)
- ADR-001 / ADR-006 in [architecture-decision-records](../../../../../docs/architecture-decision-records.md)
