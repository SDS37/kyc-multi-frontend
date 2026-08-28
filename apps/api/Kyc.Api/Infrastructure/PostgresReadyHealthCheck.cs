using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Kyc.Api.Infrastructure;

/// <summary>
/// Readiness probe: Postgres is reachable. Keep this off <c>/health</c> so liveness stays a process check.
/// Connection/command timeouts are short so orchestrators are not blocked on the EF command timeout.
/// Failures are logged without the connection string or exception text (KYC-104).
/// </summary>
public sealed class PostgresReadyHealthCheck(
    string connectionString,
    ILogger<PostgresReadyHealthCheck> logger) : IHealthCheck
{
    public const int ProbeTimeoutSeconds = 2;
    public const string UnreachableLog = "Readiness check failed: Postgres is unreachable";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = ProbeTimeoutSeconds,
                CommandTimeout = ProbeTimeoutSeconds
            };

            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection)
            {
                CommandTimeout = ProbeTimeoutSeconds
            };
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            logger.LogWarning("{Message} ({ExceptionType})", UnreachableLog, ex.GetType().Name);
            return HealthCheckResult.Unhealthy("Postgres is unreachable.");
        }
    }
}
