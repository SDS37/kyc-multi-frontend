namespace Kyc.Api.Domain.Identity;

/// <summary>
/// Single-use invite for <c>registerTenant</c>. Not tenant-scoped: the tenant does not exist until redeem.
/// </summary>
public class RegistrationInvite
{
    public Guid Id { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public Guid? RedeemedTenantId { get; set; }
}
