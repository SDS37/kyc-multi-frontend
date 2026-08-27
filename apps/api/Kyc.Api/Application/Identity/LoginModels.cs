namespace Kyc.Api.Application.Identity;

public sealed record LoginRequest(
    string TenantSlug,
    string Email,
    string Password);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds);
