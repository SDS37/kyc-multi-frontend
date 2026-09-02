namespace Kyc.Api.Infrastructure;

/// <summary>
/// Browser origins allowed to call the API (KYC-091). Empty = CORS not registered.
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "KycUi";

    /// <summary>Exact origins (scheme + host + port). No trailing slash.</summary>
    public string[] AllowedOrigins { get; set; } = [];
}
