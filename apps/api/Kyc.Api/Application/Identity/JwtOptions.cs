namespace Kyc.Api.Application.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC signing key. Must be at least 32 characters.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "kyc-api";

    public string Audience { get; set; } = "kyc-clients";

    /// <summary>Access token lifetime. MVP uses a short-lived JWT (no refresh).</summary>
    public int ExpiresMinutes { get; set; } = 60;
}
