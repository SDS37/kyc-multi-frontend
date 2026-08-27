using Kyc.Api.Domain.Identity;

namespace Kyc.Api.Application.Identity;

/// <summary>
/// Role claim values issued in JWTs and used by Hot Chocolate <c>[Authorize(Roles = ...)]</c>.
/// Keep in sync with <see cref="UserRole"/> enum names.
/// </summary>
public static class AuthRoles
{
    public const string TenantAdmin = nameof(UserRole.TenantAdmin);
    public const string Reviewer = nameof(UserRole.Reviewer);
    public const string Customer = nameof(UserRole.Customer);
}
