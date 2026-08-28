using Kyc.Api.Domain.Cases;

namespace Kyc.Api.Application.Cases;

/// <summary>
/// Client input for creating a draft case. Tenant and customer are taken from the JWT (ADR-007).
/// </summary>
public sealed record CreateDraftCaseRequest(string Title, string? FormData);

public sealed record CreateDraftCaseResponse(
    Guid Id,
    string Title,
    CaseStatus Status,
    string FormData,
    Guid TenantId,
    Guid CustomerUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
