using FluentValidation;
using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Application.Validation;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class UpdateDraftCaseService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IValidator<UpdateDraftCaseRequest> validator)
{
    public const string NotFoundMessage = "Case was not found.";
    public const string NotDraftMessage = "Only draft cases can be updated.";

    public async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> UpdateAsync(
        UpdateDraftCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var idErrors = RequestValidation.Errors(validator, request);
        if (idErrors.Count > 0)
        {
            return (null, idErrors, false, null, null);
        }

        var tenantId = currentTenant.TenantId;
        var customerUserId = currentUser.UserId;
        if (tenantId is null || customerUserId is null)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        var allowed = await CallerAuthorization.EnsureUserWithRolesAsync(
            db,
            tenantId.Value,
            customerUserId.Value,
            currentUser.Role,
            [UserRole.Customer],
            cancellationToken);
        if (!allowed)
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

        var validationErrors = RequestValidation.Errors(validator, request, RequestValidation.PayloadSet);
        if (validationErrors.Count > 0)
        {
            return (null, validationErrors, false, null, null);
        }

        var title = request.Title.Trim();
        // null FormData = leave unchanged (do not SET the read-time snapshot — a concurrent
        // FormData save would otherwise be overwritten). Whitespace / provided value like create.
        string formData;
        var replaceFormData = false;
        if (request.FormData is { } providedFormData)
        {
            formData = CaseDraftValidation.NormalizeFormData(providedFormData);
            replaceFormData = true;
        }
        else
        {
            formData = entity.FormData;
        }
        var now = DateTimeOffset.UtcNow;
        var draftRow = db.Cases.Where(c =>
            c.Id == entity.Id &&
            c.CustomerUserId == customerUserId.Value &&
            c.Status == CaseStatus.Draft);
        var rows = await AuditRecorder.ExecuteUpdateWithAuditAsync(
            db,
            ct => replaceFormData
                ? draftRow.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.Title, title)
                        .SetProperty(c => c.FormData, formData)
                        .SetProperty(c => c.UpdatedAt, now),
                    ct)
                : draftRow.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.Title, title)
                        .SetProperty(c => c.UpdatedAt, now),
                    ct),
            () => AuditRecorder.Append(
                db,
                tenantId.Value,
                customerUserId.Value,
                AuditEntityTypes.Case,
                entity.Id,
                AuditActions.CaseUpdated,
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
        entity.Title = title;
        entity.FormData = formData;
        entity.UpdatedAt = now;
        var customerEmail = await CreateDraftCaseService.GetCustomerEmailAsync(
            db,
            customerUserId.Value,
            cancellationToken);
        return (CreateDraftCaseService.ToResponse(entity, customerEmail), Array.Empty<string>(), false, null, null);
    }
}
