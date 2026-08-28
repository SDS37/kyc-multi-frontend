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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
