using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Kyc.Api.Infrastructure;

/// <summary>
/// Readiness probe: Postgres is reachable. Keep this off <c>/health</c> so liveness stays a process check.
/// Connection/command timeouts are short so orchestrators are not blocked on the EF command timeout.
/// </summary>
public sealed class PostgresReadyHealthCheck(string connectionString) : IHealthCheck
{
    internal const int ProbeTimeoutSeconds = 2;

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
            return HealthCheckResult.Unhealthy("Postgres is unreachable.", ex);
        }
    }
}
