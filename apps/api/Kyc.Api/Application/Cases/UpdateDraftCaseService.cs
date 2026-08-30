using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class UpdateDraftCaseService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    public const string NotFoundMessage = "Case was not found.";
    public const string NotDraftMessage = "Only draft cases can be updated.";

    public async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> UpdateAsync(
        UpdateDraftCaseRequest request,
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

        // Same stale-token guard as create: JWT subject must still exist in this tenant.
        var customerExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == customerUserId && u.TenantId == tenantId,
                cancellationToken);

        if (!customerExists)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        // Tenant filter applies — other tenants never see the row.
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

        var validationErrors = CaseDraftValidation.ValidateTitleAndFormData(request.Title, request.FormData);
        if (validationErrors.Count > 0)
        {
            return (null, validationErrors, false, null, null);
        }

        entity.Title = request.Title.Trim();
        // null FormData = leave unchanged; whitespace / provided value normalized like create.
        if (request.FormData is not null)
        {
            entity.FormData = CaseDraftValidation.NormalizeFormData(request.FormData);
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        AuditRecorder.Append(
            db,
            tenantId.Value,
            customerUserId.Value,
            AuditEntityTypes.Case,
            entity.Id,
            AuditActions.CaseUpdated,
            entity.UpdatedAt);
        await db.SaveChangesAsync(cancellationToken);

        return (CreateDraftCaseService.ToResponse(entity), Array.Empty<string>(), false, null, null);
    }
}
