# Study: `Application/Audit`

Study tour of this folder. Parent: [../README.STUDY.md](../README.STUDY.md).

**Aligned with:** `main` after KYC-051 (case audit history).

## Purpose

Append-only **write** of audit rows (KYC-050) and Reviewer/TenantAdmin **read** of a case’s history (KYC-051). No update/delete API.

## Why these files exist

| File | Role |
|---|---|
| `AuditRecorder` | Static helper: `Append` before `SaveChanges`, or `ExecuteUpdateWithAuditAsync` when the domain write uses `ExecuteUpdateAsync`. |
| `ListCaseAuditService` | KYC-051: Reviewer/TenantAdmin only; case + document-upload rows for the case; newest first. |
| `CaseAuditEntryResponse` | GraphQL DTO (id, entityType, entityId, action, actorUserId, occurredAt, payload). |

## Actions recorded (write)

| Action | EntityType | Service |
|---|---|---|
| `CaseCreated` | `Case` | `CreateDraftCaseService` |
| `CaseUpdated` | `Case` | `UpdateDraftCaseService` |
| `CaseSubmitted` | `Case` | `SubmitCaseService` |
| `ReviewStarted` | `Case` | `StartCaseReviewService` |
| `CaseApproved` / `CaseRejected` | `Case` | `CompleteCaseReviewService` |
| `DocumentUploaded` | `Document` | `UploadDocumentService` |

## Read (KYC-051)

GraphQL: `caseAuditEntries(caseId)` with `[Authorize(Roles = Reviewer, TenantAdmin)]`.

Includes:
- rows where `EntityType=Case` and `EntityId=caseId`
- `DocumentUploaded` rows for documents belonging to that case

Customers → `AUTH_NOT_AUTHORIZED`. Missing/other-tenant case → `NOT_FOUND`.

## What to skip

- Update/delete endpoints for audit.
- MediatR / domain events bus for MVP.

## Links

- Domain `AuditEntry` / `AuditActions`
- [dotnet-code-standards](../../../../../docs/dotnet-code-standards.md)
