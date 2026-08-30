using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Audit;

/// <summary>
/// Case audit history for Reviewer / TenantAdmin (KYC-051). Includes case lifecycle rows and
/// document-upload rows for documents on the case. Newest first. Customers are forbidden.
/// </summary>
public sealed class ListCaseAuditService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    public const string ForbiddenMessage = "Only reviewers and tenant admins can view case audit entries.";
    public const string NotFoundMessage = CaseVisibility.NotFoundMessage;

    public async Task<(IReadOnlyList<CaseAuditEntryResponse>? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, bool Forbidden, string? ErrorCode, string? ErrorMessage)> ListAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return (null, ["Case id is required."], false, false, null, null);
        }

        var tenantId = currentTenant.TenantId;
        var userId = currentUser.UserId;
        var role = currentUser.Role;
        if (tenantId is null || userId is null || role is null)
        {
            return (null, Array.Empty<string>(), true, false, null, null);
        }

        if (role is not (UserRole.Reviewer or UserRole.TenantAdmin))
        {
            return (null, Array.Empty<string>(), false, true, null, null);
        }

        var userExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == userId && u.TenantId == tenantId,
                cancellationToken);

        if (!userExists)
        {
            return (null, Array.Empty<string>(), true, false, null, null);
        }

        // Tenant filter + reviewer/admin see all tenant cases (CaseVisibility).
        var caseExists = await CaseVisibility
            .ApplyRoleFilter(db.Cases.AsNoTracking(), role.Value, userId.Value)
            .AnyAsync(c => c.Id == caseId, cancellationToken);

        if (!caseExists)
        {
            return (null, Array.Empty<string>(), false, false, "NOT_FOUND", NotFoundMessage);
        }

        var documentIds = await db.Documents
            .AsNoTracking()
            .Where(d => d.CaseId == caseId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var entries = await db.AuditEntries
            .AsNoTracking()
            .Where(e =>
                (e.EntityType == AuditEntityTypes.Case && e.EntityId == caseId) ||
                (e.EntityType == AuditEntityTypes.Document && documentIds.Contains(e.EntityId)))
            .Select(e => new CaseAuditEntryResponse(
                e.Id,
                e.EntityType,
                e.EntityId,
                e.Action,
                e.ActorUserId,
                e.OccurredAt,
                e.Payload))
            .ToListAsync(cancellationToken);

        // Order in memory so SQLite and Postgres stay consistent (same pattern as documents list).
        return (
            [.. entries
                .OrderByDescending(e => e.OccurredAt)
                .ThenByDescending(e => e.Id)],
            Array.Empty<string>(),
            false,
            false,
            null,
            null);
    }
}
