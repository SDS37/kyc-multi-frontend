using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Identity;

/// <summary>
/// Defense-in-depth: JWT role must match the persisted user row and be in the allowed set.
/// </summary>
public static class CallerAuthorization
{
    /// <summary>
    /// Returns false when the caller is missing, the JWT role is wrong, the user row is missing,
    /// or the database role does not match the JWT (e.g. demotion with a still-valid token).
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

        var row = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.TenantId == tenantId)
            .Select(u => new { u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return false;
        }

        return row.Role == jwtRole.Value && allowedRoles.Contains(row.Role);
    }
}
