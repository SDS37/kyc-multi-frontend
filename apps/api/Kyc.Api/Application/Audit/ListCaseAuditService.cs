using FluentValidation;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Application.Validation;
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
    ICurrentUser currentUser,
    IValidator<CaseIdInput> caseIdValidator)
{
    public const string ForbiddenMessage = "Only reviewers and tenant admins can view case audit entries.";
    public const string NotFoundMessage = CaseVisibility.NotFoundMessage;

    public async Task<(IReadOnlyList<CaseAuditEntryResponse>? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, bool Forbidden, string? ErrorCode, string? ErrorMessage)> ListAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var idErrors = RequestValidation.Errors(caseIdValidator, new CaseIdInput(caseId));
        if (idErrors.Count > 0)
        {
            return (null, idErrors, false, false, null, null);
        }

        // Resolve caller (JWT + DB user) before role gate so deleted users get AUTH_FAILED, not Forbidden.
        var (userId, role, unauthorized) = await CaseVisibility.ResolveCallerAsync(
            db,
            currentTenant,
            currentUser,
            cancellationToken);

        if (unauthorized)
        {
            return (null, Array.Empty<string>(), true, false, null, null);
        }

        if (role is not (UserRole.Reviewer or UserRole.TenantAdmin))
        {
            return (null, Array.Empty<string>(), false, true, null, null);
        }

        // Tenant filter + reviewer/admin see all tenant cases (CaseVisibility).
        var caseExists = await CaseVisibility
            .ApplyRoleFilter(db.Cases.AsNoTracking(), role, userId)
            .AnyAsync(c => c.Id == caseId, cancellationToken);

        if (!caseExists)
        {
            return (null, Array.Empty<string>(), false, false, "NOT_FOUND", NotFoundMessage);
        }

        // Keep as IQueryable so EF emits a subquery (no client-side IN list).
        var documentIds = db.Documents
            .AsNoTracking()
            .Where(d => d.CaseId == caseId)
            .Select(d => d.Id);

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
