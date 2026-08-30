using Kyc.Api.Domain.Identity;

namespace Kyc.Api.Domain.Audit;

/// <summary>
/// Append-only audit row (KYC-050). Tenant-scoped; never updated or deleted by application APIs.
/// </summary>
public class AuditEntry : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid ActorUserId { get; set; }
    public User ActorUser { get; set; } = null!;

    /// <summary>Domain type name, e.g. <c>Case</c> or <c>Document</c>.</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>Action name from <see cref="AuditActions"/>.</summary>
    public string Action { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Optional JSON metadata — never secrets or storage keys.</summary>
    public string? Payload { get; set; }
}
