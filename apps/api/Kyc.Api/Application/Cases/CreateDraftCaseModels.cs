using Kyc.Api.Domain.Cases;

namespace Kyc.Api.Application.Cases;

/// <summary>
/// Client input for creating a draft case. Tenant and customer are taken from the JWT (ADR-007).
/// </summary>
public sealed record CreateDraftCaseRequest(string Title, string? FormData);

/// <summary>
/// Client input for updating a draft. Owner and tenant come from the JWT; only <see cref="CaseStatus.Draft"/> may change.
/// </summary>
public sealed record UpdateDraftCaseRequest(Guid Id, string Title, string? FormData);

/// <summary>Shared GraphQL/API payload for a persisted case.</summary>
public sealed record CaseResponse(
    Guid Id,
    string Title,
    CaseStatus Status,
    string FormData,
    Guid TenantId,
    Guid CustomerUserId,
    string CustomerEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedBy,
    string? ReviewComment);

/// <summary>Customer submits a draft by id (KYC-033). Form is read from persisted FormData.</summary>
public sealed record SubmitCaseRequest(Guid Id);

/// <summary>Reviewer/TenantAdmin starts review on a submitted case (KYC-034).</summary>
public sealed record StartCaseReviewRequest(Guid Id);

/// <summary>Reviewer/TenantAdmin approves an InReview case (KYC-035). Comment optional.</summary>
public sealed record ApproveCaseRequest(Guid Id, string? Comment);

/// <summary>Reviewer/TenantAdmin rejects an InReview case (KYC-035). Comment required.</summary>
public sealed record RejectCaseRequest(Guid Id, string Comment);

/// <summary>
/// List cases visible to the caller (KYC-036). Tenant from JWT; ownership depends on role.
/// <see cref="Skip"/> defaults to 0; <see cref="Take"/> defaults to <see cref="ListCasesService.DefaultPageSize"/>.
/// </summary>
public sealed record ListCasesRequest(CaseStatus? Status, int? Skip, int? Take);

/// <summary>List row without FormData (KYC-036) — keeps list responses small.</summary>
public sealed record CaseListItemResponse(
    Guid Id,
    string Title,
    CaseStatus Status,
    Guid TenantId,
    Guid CustomerUserId,
    string CustomerEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedBy,
    string? ReviewComment);

/// <summary>Paginated case list for GraphQL <c>cases</c>.</summary>
public sealed record CaseListResponse(
    IReadOnlyList<CaseListItemResponse> Items,
    int TotalCount,
    int Skip,
    int Take);

/// <summary>
/// Review comment exposed on case detail (KYC-037). Today sourced from <c>Case.ReviewComment</c>;
/// a comment thread can replace this later without changing the GraphQL field name.
/// </summary>
public sealed record CaseCommentResponse(
    string Text,
    DateTimeOffset? CreatedAt,
    Guid? AuthorUserId);

/// <summary>
/// Document metadata only — never file bytes (KYC-037 / KYC-040+).
/// </summary>
public sealed record CaseDocumentMetadataResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    Guid UploadedBy);

/// <summary>Case detail payload: full case, comments, document metadata (KYC-037).</summary>
public sealed record CaseDetailResponse(
    CaseResponse Case,
    IReadOnlyList<CaseCommentResponse> Comments,
    IReadOnlyList<CaseDocumentMetadataResponse> Documents);

