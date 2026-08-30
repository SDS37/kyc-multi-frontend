namespace Kyc.Api.Domain.Audit;

/// <summary>KYC-050 action names persisted on <see cref="AuditEntry.Action"/>.</summary>
public static class AuditActions
{
    public const string CaseCreated = "CaseCreated";
    public const string CaseUpdated = "CaseUpdated";
    public const string CaseSubmitted = "CaseSubmitted";
    public const string ReviewStarted = "ReviewStarted";
    public const string CaseApproved = "CaseApproved";
    public const string CaseRejected = "CaseRejected";
    public const string DocumentUploaded = "DocumentUploaded";
}

/// <summary>KYC-050 entity type names persisted on <see cref="AuditEntry.EntityType"/>.</summary>
public static class AuditEntityTypes
{
    public const string Case = "Case";
    public const string Document = "Document";
}
