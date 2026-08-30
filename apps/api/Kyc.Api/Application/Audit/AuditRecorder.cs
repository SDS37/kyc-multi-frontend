using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Audit;

/// <summary>
/// Append-only audit writes (KYC-050). Callers add via <see cref="Append"/> before
/// <c>SaveChangesAsync</c>, or use <see cref="ExecuteUpdateWithAuditAsync"/> when the
/// domain write uses <c>ExecuteUpdateAsync</c>.
/// </summary>
public static class AuditRecorder
{
    public static void Append(
        AppDbContext db,
        Guid tenantId,
        Guid actorUserId,
        string entityType,
        Guid entityId,
        string action,
        DateTimeOffset occurredAt,
        string? payload = null)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorUserId = actorUserId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OccurredAt = occurredAt,
            Payload = payload
        });
    }

    /// <summary>
    /// Runs an <c>ExecuteUpdateAsync</c> and, on success, persists an audit row in the same transaction.
    /// Uses the EF execution strategy so this works with Npgsql retry-on-failure.
    /// </summary>
    public static async Task<int> ExecuteUpdateWithAuditAsync(
        AppDbContext db,
        Func<CancellationToken, Task<int>> executeUpdate,
        Action appendAudit,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Retries must not reuse tracked entities from a failed attempt (same as RegisterTenantService).
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var rows = await executeUpdate(cancellationToken);
            if (rows > 0)
            {
                appendAudit();
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return rows;
        });
    }
}
