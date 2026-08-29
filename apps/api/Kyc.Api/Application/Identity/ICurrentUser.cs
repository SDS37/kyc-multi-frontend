using Kyc.Api.Domain.Identity;

namespace Kyc.Api.Application.Identity;

/// <summary>
/// Authenticated user for the current request. Resolved from JWT claims (never from client input).
/// Null members when unauthenticated or claims are missing/invalid.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>JWT <c>role</c> claim as <see cref="UserRole"/>.</summary>
    UserRole? Role { get; }
}
