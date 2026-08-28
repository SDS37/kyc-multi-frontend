namespace Kyc.Api.Infrastructure;

/// <summary>
/// EF / Npgsql retries and ASP.NET request timeout (KYC-103).
/// Values live in <c>Resilience</c> configuration; defaults match <c>appsettings.json</c>.
/// </summary>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>Npgsql command timeout for EF Core commands (seconds).</summary>
    public int NpgsqlCommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Transient-failure retries for Npgsql (<c>EnableRetryOnFailure</c>).</summary>
    public int EfMaxRetryCount { get; set; } = 5;

    public int EfMaxRetryDelaySeconds { get; set; } = 10;

    /// <summary>
    /// ASP.NET request-timeout middleware default policy (seconds).
    /// Cooperative: observers must honor <c>HttpContext.RequestAborted</c>.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 60;

    public void Validate()
    {
        if (NpgsqlCommandTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Resilience:NpgsqlCommandTimeoutSeconds must be greater than 0.");
        }

        if (EfMaxRetryCount < 0)
        {
            throw new InvalidOperationException("Resilience:EfMaxRetryCount must be 0 or greater.");
        }

        if (EfMaxRetryDelaySeconds <= 0)
        {
            throw new InvalidOperationException("Resilience:EfMaxRetryDelaySeconds must be greater than 0.");
        }

        if (RequestTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Resilience:RequestTimeoutSeconds must be greater than 0.");
        }
    }
}
