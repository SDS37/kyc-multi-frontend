using FluentValidation;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Application.Validation;
using Kyc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class GetCaseDetailService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IValidator<CaseIdInput> caseIdValidator)
{
    public async Task<(CaseDetailResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var idErrors = RequestValidation.Errors(caseIdValidator, new CaseIdInput(id));
        if (idErrors.Count > 0)
        {
            return (null, idErrors, false, null, null);
        }

        var (userId, role, unauthorized) = await CaseVisibility.ResolveCallerAsync(
            db,
            currentTenant,
            currentUser,
            cancellationToken);

        if (unauthorized)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        // Tenant filter + role ownership (same rules as list).
        var entity = await CaseVisibility
            .ApplyRoleFilter(db.Cases.AsNoTracking(), role, userId)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (entity is null)
        {
            return (null, Array.Empty<string>(), false, "NOT_FOUND", CaseVisibility.NotFoundMessage);
        }

        var customerEmail = await CreateDraftCaseService.GetCustomerEmailAsync(
            db,
            entity.CustomerUserId,
            cancellationToken);
        var caseResponse = CreateDraftCaseService.ToResponse(entity, customerEmail);
        IReadOnlyList<CaseCommentResponse> comments = string.IsNullOrWhiteSpace(entity.ReviewComment)
            ? Array.Empty<CaseCommentResponse>()
            : [new CaseCommentResponse(entity.ReviewComment.Trim(), entity.ReviewedAt, entity.ReviewedBy)];

        var documents = await ListDocumentsService.LoadMetadataAsync(db, entity.Id, cancellationToken);

        return (
            new CaseDetailResponse(caseResponse, comments, documents),
            Array.Empty<string>(),
            false,
            null,
            null);
    }
}
