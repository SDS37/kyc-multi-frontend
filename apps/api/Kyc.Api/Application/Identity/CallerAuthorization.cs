using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Identity;

/// <summary>
/// Defense-in-depth: JWT role must match the persisted user row, the tenant must be active,
/// and the role must be in the allowed set.
/// </summary>
public static class CallerAuthorization
{
    /// <summary>
    /// Returns false when the caller is missing, the JWT role is wrong, the user row is missing,
    /// the tenant is inactive, or the database role does not match the JWT
    /// (e.g. demotion with a still-valid token).
    /// </summary>
    public static async Task<bool> EnsureUserWithRolesAsync(
        AppDbContext db,
        Guid tenantId,
        Guid userId,
        UserRole? jwtRole,
        IReadOnlyCollection<UserRole> allowedRoles,
        CancellationToken cancellationToken)
    {
        if (jwtRole is null || !allowedRoles.Contains(jwtRole.Value))
        {
            return false;
        }

        var row = await LoadCallerRowAsync(db, tenantId, userId, cancellationToken);
        if (row?.TenantActive is not true)
        {
            return false;
        }

        return row.Role == jwtRole.Value && allowedRoles.Contains(row.Role);
    }

    /// <summary>
    /// Resolves an authenticated caller for reads: JWT role must match DB role and tenant must be active.
    /// Returns the <strong>database</strong> role for visibility filtering.
    /// </summary>
    public static async Task<(Guid UserId, UserRole Role, bool Unauthorized)> ResolveActiveCallerAsync(
        AppDbContext db,
        Guid? tenantId,
        Guid? userId,
        UserRole? jwtRole,
        CancellationToken cancellationToken)
    {
        if (tenantId is null || userId is null || jwtRole is null)
        {
            return (default, default, true);
        }

        var row = await LoadCallerRowAsync(db, tenantId.Value, userId.Value, cancellationToken);
        if (row?.TenantActive is not true || row.Role != jwtRole.Value)
        {
            return (default, default, true);
        }

        return row.Role switch
        {
            UserRole.Customer or UserRole.Reviewer or UserRole.TenantAdmin =>
                (userId.Value, row.Role, false),
            _ => (default, default, true)
        };
    }

    private static Task<CallerRow?> LoadCallerRowAsync(
        AppDbContext db,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.TenantId == tenantId)
            .Select(u => new CallerRow(u.Role, u.Tenant.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record CallerRow(UserRole Role, bool TenantActive);
}
