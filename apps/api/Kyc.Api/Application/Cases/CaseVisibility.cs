using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

/// <summary>
/// Shared case visibility for list/detail (KYC-036 / KYC-037).
/// Tenant scope comes from EF <c>ITenantScoped</c> filters (ADR-007); this layer adds role ownership.
/// </summary>
public static class CaseVisibility
{
    public const string NotFoundMessage = "Case was not found.";

    public static async Task<(Guid UserId, UserRole Role, bool Unauthorized)> ResolveCallerAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var tenantId = currentTenant.TenantId;
        var userId = currentUser.UserId;
        var role = currentUser.Role;
        if (tenantId is null || userId is null || role is null)
        {
            return (default, default, true);
        }

        var userExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == userId && u.TenantId == tenantId,
                cancellationToken);

        if (!userExists)
        {
            return (default, default, true);
        }

        return role.Value switch
        {
            UserRole.Customer or UserRole.Reviewer or UserRole.TenantAdmin =>
                (userId.Value, role.Value, false),
            _ => (default, default, true)
        };
    }

    public static IQueryable<Case> ApplyRoleFilter(IQueryable<Case> query, UserRole role, Guid userId) =>
        role switch
        {
            UserRole.Customer => query.Where(c => c.CustomerUserId == userId),
            UserRole.Reviewer or UserRole.TenantAdmin => query,
            _ => query.Where(_ => false)
        };
}
