namespace Kyc.Api.Application.Identity;

public sealed record RegisterTenantRequest(
    string TenantName,
    string TenantSlug,
    string AdminEmail,
    string AdminPassword,
    string? InviteCode = null,
    string? CaptchaToken = null);

public sealed record RegisterTenantResponse(
    Guid TenantId,
    string TenantSlug,
    Guid AdminUserId,
    string AdminEmail);
