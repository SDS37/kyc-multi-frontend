namespace Kyc.Api.Domain;

/// <summary>
/// Marker for entities that belong to a single tenant.
/// EF applies a global query filter so queries only see the current JWT tenant (ADR-007 / KYC-014).
/// Case (KYC-030) and other tenant-owned types must implement this.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}
