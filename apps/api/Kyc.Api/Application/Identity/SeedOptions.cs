namespace Kyc.Api.Application.Identity;

/// <summary>Development demo seed (KYC-101). Ignored outside Development even when enabled.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>When true in Development, startup inserts Acme + Globex demo rows if missing.</summary>
    public bool Enabled { get; set; }
}
