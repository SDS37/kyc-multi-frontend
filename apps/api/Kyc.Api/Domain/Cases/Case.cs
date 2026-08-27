using Kyc.Api.Domain;
using Kyc.Api.Domain.Identity;

namespace Kyc.Api.Domain.Cases;

/// <summary>
/// KYC case owned by one tenant and one customer (KYC-030).
/// Implements <see cref="ITenantScoped"/> so EF tenant filters from KYC-014 apply.
/// </summary>
public class Case : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid CustomerUserId { get; set; }
    public User CustomerUser { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public CaseStatus Status { get; set; }

    /// <summary>JSON document for MVP form payload.</summary>
    public string FormData { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }
    public User? Reviewer { get; set; }
}
