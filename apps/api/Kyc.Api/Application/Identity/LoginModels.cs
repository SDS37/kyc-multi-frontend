namespace Kyc.Api.Application.Identity;

public sealed record LoginRequest(
    string TenantSlug,
    string Email,
    string Password,
    string? CaptchaToken = null);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds);
