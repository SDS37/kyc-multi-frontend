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
        var idErrors = RequestValidation.Errors(validator, request, RequestValidation.IdSet);
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

        var customerEmail = await CreateDraftCaseService.GetCustomerEmailAsync(
            db,
            customerUserId.Value,
            cancellationToken);
        return (CreateDraftCaseService.ToResponse(entity, customerEmail), Array.Empty<string>(), false, null, null);
    }
}
