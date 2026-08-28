namespace Kyc.Api.Application.Identity;

/// <summary>
/// Authenticated user for the current request. Resolved from the JWT <c>sub</c> claim (never from client input).
/// Null when unauthenticated.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}
