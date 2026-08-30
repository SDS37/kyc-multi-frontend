namespace Kyc.Api.Application.Audit;

/// <summary>One audit row for GraphQL case history (KYC-051). Never includes storage keys.</summary>
public sealed record CaseAuditEntryResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid ActorUserId,
    DateTimeOffset OccurredAt,
    string? Payload);
