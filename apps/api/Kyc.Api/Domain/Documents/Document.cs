using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;

namespace Kyc.Api.Domain.Documents;

/// <summary>
/// Case attachment metadata (KYC-040). Bytes live in object storage; this row is tenant-scoped.
/// </summary>
public class Document : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid CaseId { get; set; }
    public Case Case { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Opaque object-store key — never expose to clients.</summary>
    public string StorageKey { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    public DateTimeOffset UploadedAt { get; set; }
}
