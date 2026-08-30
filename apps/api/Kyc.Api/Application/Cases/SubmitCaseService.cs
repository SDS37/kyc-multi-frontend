using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class SubmitCaseService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    public const string NotFoundMessage = "Case was not found.";
    public const string NotDraftMessage = "Only draft cases can be submitted.";

    public async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> SubmitAsync(
        SubmitCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id == Guid.Empty)
        {
            return (null, ["Case id is required."], false, null, null);
        }

        var tenantId = currentTenant.TenantId;
        var customerUserId = currentUser.UserId;
        if (tenantId is null || customerUserId is null)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        var customerExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == customerUserId && u.TenantId == tenantId,
                cancellationToken);

        if (!customerExists)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        var entity = await db.Cases
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (entity is null || entity.CustomerUserId != customerUserId.Value)
        {
            return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
        }

        if (entity.Status != CaseStatus.Draft)
        {
            return (null, Array.Empty<string>(), false, "DOMAIN", NotDraftMessage);
        }

        var formErrors = CaseDraftValidation.ValidateSubmitFormData(entity.FormData);
        if (formErrors.Count > 0)
        {
            return (null, formErrors, false, null, null);
        }

        var now = DateTimeOffset.UtcNow;
        var rows = await AuditRecorder.ExecuteUpdateWithAuditAsync(
            db,
            ct => db.Cases
                .Where(c =>
                    c.Id == entity.Id &&
                    c.CustomerUserId == customerUserId.Value &&
                    c.Status == CaseStatus.Draft)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.Status, CaseStatus.Submitted)
                        .SetProperty(c => c.SubmittedAt, now)
                        .SetProperty(c => c.UpdatedAt, now),
                    ct),
            () => AuditRecorder.Append(
                db,
                tenantId.Value,
                customerUserId.Value,
                AuditEntityTypes.Case,
                entity.Id,
                AuditActions.CaseSubmitted,
                now),
            cancellationToken);

        if (rows == 0)
        {
            db.Entry(entity).State = EntityState.Detached;
            var current = await db.Cases
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == entity.Id, cancellationToken);
            if (current is null || current.CustomerUserId != customerUserId.Value)
            {
                return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
            }

            return (null, Array.Empty<string>(), false, "DOMAIN", NotDraftMessage);
        }

        db.Entry(entity).State = EntityState.Detached;
        entity.Status = CaseStatus.Submitted;
        entity.SubmittedAt = now;
        entity.UpdatedAt = now;
        return (CreateDraftCaseService.ToResponse(entity), Array.Empty<string>(), false, null, null);
    }
}
