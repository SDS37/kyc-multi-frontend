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

public sealed class SubmitCaseService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IValidator<SubmitCaseRequest> validator)
{
    public const string NotFoundMessage = "Case was not found.";
    public const string NotDraftMessage = "Only draft cases can be submitted.";
    public const string DraftChangedMessage = "The draft changed while submitting. Submit again.";

    public async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> SubmitAsync(
        SubmitCaseRequest request,
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

        // Snapshot is part of the CAS. A concurrent updateDraftCase must not leave Submitted + different FormData.
        var acceptedFormData = entity.FormData;
        var formErrors = CaseDraftValidation.ValidateSubmitFormData(acceptedFormData);
        if (formErrors.Count > 0)
        {
            return (null, formErrors, false, null, null);
        }

        var now = DateTimeOffset.UtcNow;
        var rows = await PersistSubmittedAsync(
            tenantId.Value,
            customerUserId.Value,
            entity.Id,
            acceptedFormData,
            now,
            cancellationToken);

        if (rows == 0)
        {
            return await LostSubmitRaceAsync(entity, customerUserId.Value, cancellationToken);
        }

        db.Entry(entity).State = EntityState.Detached;
        entity.Status = CaseStatus.Submitted;
        entity.SubmittedAt = now;
        entity.UpdatedAt = now;
        var customerEmail = await CreateDraftCaseService.GetCustomerEmailAsync(
            db,
            customerUserId.Value,
            cancellationToken);
        return (CreateDraftCaseService.ToResponse(entity, customerEmail), Array.Empty<string>(), false, null, null);
    }

    /// <summary>
    /// Draft → Submitted only when the row is still the validated FormData snapshot (KYC-111).
    /// </summary>
    private Task<int> PersistSubmittedAsync(
        Guid tenantId,
        Guid customerUserId,
        Guid caseId,
        string acceptedFormData,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        AuditRecorder.ExecuteUpdateWithAuditAsync(
            db,
            ct => db.Cases
                .Where(c =>
                    c.Id == caseId &&
                    c.CustomerUserId == customerUserId &&
                    c.Status == CaseStatus.Draft &&
                    c.FormData == acceptedFormData)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.Status, CaseStatus.Submitted)
                        .SetProperty(c => c.SubmittedAt, now)
                        .SetProperty(c => c.UpdatedAt, now),
                    ct),
            () => AuditRecorder.Append(
                db,
                tenantId,
                customerUserId,
                AuditEntityTypes.Case,
                caseId,
                AuditActions.CaseSubmitted,
                now),
            cancellationToken);

    /// <summary>
    /// 0-row CAS: missing owner stays <c>NOT_FOUND</c>; leaving Draft stays <c>DOMAIN</c>.
    /// A still-draft row lost on FormData is re-validated (invalid → <c>VALIDATION</c>, still valid → <c>DOMAIN</c>).
    /// </summary>
    private async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> LostSubmitRaceAsync(
        Case entity,
        Guid customerUserId,
        CancellationToken cancellationToken)
    {
        db.Entry(entity).State = EntityState.Detached;
        var current = await db.Cases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == entity.Id, cancellationToken);
        if (current is null || current.CustomerUserId != customerUserId)
        {
            return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
        }

        if (current.Status != CaseStatus.Draft)
        {
            return (null, Array.Empty<string>(), false, "DOMAIN", NotDraftMessage);
        }

        var formErrors = CaseDraftValidation.ValidateSubmitFormData(current.FormData);
        if (formErrors.Count > 0)
        {
            return (null, formErrors, false, null, null);
        }

        return (null, Array.Empty<string>(), false, "DOMAIN", DraftChangedMessage);
    }
}
