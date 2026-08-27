namespace Kyc.Api.Application.Tenancy;

/// <summary>
/// Tenant context for the current request. Resolved from the JWT <c>tenant_id</c> claim (never from client input).
/// Null when unauthenticated (public register/login).
/// </summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
}
