# Study: `Application/Audit`

Study tour of this folder. Parent: [../README.STUDY.md](../README.STUDY.md).

**Aligned with:** `feat/kyc-050-record-key-actions` / KYC-050.

## Purpose

Append-only **write** of audit rows for key case/document actions. No GraphQL/REST read or update/delete API in this story (list is a later story).

## Why these files exist

| File | Role |
|---|---|
| `AuditRecorder` | Static helper: `Append` before `SaveChanges`, or `ExecuteUpdateWithAuditAsync` when the domain write uses `ExecuteUpdateAsync` (same DB transaction + EF retry strategy). |

## Actions recorded

| Action | EntityType | Service |
|---|---|---|
| `CaseCreated` | `Case` | `CreateDraftCaseService` |
| `CaseUpdated` | `Case` | `UpdateDraftCaseService` |
| `CaseSubmitted` | `Case` | `SubmitCaseService` |
| `ReviewStarted` | `Case` | `StartCaseReviewService` |
| `CaseApproved` / `CaseRejected` | `Case` | `CompleteCaseReviewService` |
| `DocumentUploaded` | `Document` | `UploadDocumentService` |

Fields: `TenantId`, `ActorUserId`, `EntityType`, `EntityId`, `Action`, `OccurredAt`, optional `Payload` (JSON; never storage keys).

## What to skip

- Inventing update/delete endpoints for audit.
- MediatR / domain events bus for MVP — call `AuditRecorder` from the same Application service.

## Links

- Domain `AuditEntry` / `AuditActions`
- [dotnet-code-standards](../../../../../docs/dotnet-code-standards.md)
