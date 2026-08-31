using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;

namespace Kyc.Api.Application.Cases;

/// <summary>
/// Shared case visibility for list/detail (KYC-036 / KYC-037).
/// Tenant scope comes from EF <c>ITenantScoped</c> filters (ADR-007); this layer adds role ownership.
/// </summary>
public static class CaseVisibility
{
    public const string NotFoundMessage = "Case was not found.";

    public static Task<(Guid UserId, UserRole Role, bool Unauthorized)> ResolveCallerAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        CallerAuthorization.ResolveActiveCallerAsync(
            db,
            currentTenant.TenantId,
            currentUser.UserId,
            currentUser.Role,
            cancellationToken);

    public static IQueryable<Case> ApplyRoleFilter(IQueryable<Case> query, UserRole role, Guid userId) =>
        role switch
        {
            UserRole.Customer => query.Where(c => c.CustomerUserId == userId),
            UserRole.Reviewer or UserRole.TenantAdmin => query,
            _ => query.Where(_ => false)
        };
}
