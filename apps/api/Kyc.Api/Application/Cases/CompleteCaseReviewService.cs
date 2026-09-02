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

public sealed class CompleteCaseReviewService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IValidator<ApproveCaseRequest> approveValidator,
    IValidator<RejectCaseRequest> rejectValidator)
{
    public const int MaxCommentLength = 2000;
    public const string NotFoundMessage = "Case was not found.";
    public const string NotInReviewMessage = "Only cases in review can be approved or rejected.";
    public const string RejectCommentRequiredMessage = "A comment is required when rejecting a case.";

    public async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> ApproveAsync(
        ApproveCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var idErrors = RequestValidation.Errors(approveValidator, request, RequestValidation.IdSet);
        if (idErrors.Count > 0)
        {
            return (null, idErrors, false, null, null);
        }

        return await CompleteAsync(
            request.Id,
            CaseStatus.Approved,
            request.Comment,
            () => RequestValidation.Errors(approveValidator, request, RequestValidation.CommentSet),
            cancellationToken);
    }

    public async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> RejectAsync(
        RejectCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var idErrors = RequestValidation.Errors(rejectValidator, request, RequestValidation.IdSet);
        if (idErrors.Count > 0)
        {
            return (null, idErrors, false, null, null);
        }

        return await CompleteAsync(
            request.Id,
            CaseStatus.Rejected,
            request.Comment,
            () => RequestValidation.Errors(rejectValidator, request, RequestValidation.CommentSet),
            cancellationToken);
    }

    private async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> CompleteAsync(
        Guid caseId,
        CaseStatus targetStatus,
        string? comment,
        Func<IReadOnlyList<string>> commentErrors,
        CancellationToken cancellationToken)
    {

        var tenantId = currentTenant.TenantId;
        var reviewerUserId = currentUser.UserId;
        if (tenantId is null || reviewerUserId is null)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        var allowed = await CallerAuthorization.EnsureUserWithRolesAsync(
            db,
            tenantId.Value,
            reviewerUserId.Value,
            currentUser.Role,
            [UserRole.Reviewer, UserRole.TenantAdmin],
            cancellationToken);
        if (!allowed)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        var entity = await db.Cases
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (entity is null)
        {
            return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
        }

        if (entity.Status != CaseStatus.InReview)
        {
            return (null, Array.Empty<string>(), false, "DOMAIN", NotInReviewMessage);
        }

        var commentValidation = commentErrors();
        if (commentValidation.Count > 0)
        {
            return (null, commentValidation, false, null, null);
        }

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        var now = DateTimeOffset.UtcNow;
        var action = targetStatus == CaseStatus.Approved
            ? AuditActions.CaseApproved
            : AuditActions.CaseRejected;

        var rows = await AuditRecorder.ExecuteUpdateWithAuditAsync(
            db,
            ct => db.Cases
                .Where(c => c.Id == entity.Id && c.Status == CaseStatus.InReview)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.Status, targetStatus)
                        .SetProperty(c => c.ReviewedAt, now)
                        .SetProperty(c => c.ReviewedBy, reviewerUserId.Value)
                        .SetProperty(c => c.ReviewComment, normalizedComment)
                        .SetProperty(c => c.UpdatedAt, now),
                    ct),
            () => AuditRecorder.Append(
                db,
                tenantId.Value,
                reviewerUserId.Value,
                AuditEntityTypes.Case,
                entity.Id,
                action,
                now),
            cancellationToken);

        if (rows == 0)
        {
            db.Entry(entity).State = EntityState.Detached;
            var current = await db.Cases
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == entity.Id, cancellationToken);
            if (current is null)
            {
                return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
            }

            return (null, Array.Empty<string>(), false, "DOMAIN", NotInReviewMessage);
        }

        db.Entry(entity).State = EntityState.Detached;
        entity.Status = targetStatus;
        entity.ReviewedAt = now;
        entity.ReviewedBy = reviewerUserId.Value;
        entity.ReviewComment = normalizedComment;
        entity.UpdatedAt = now;
        var customerEmail = await CreateDraftCaseService.GetCustomerEmailAsync(
            db,
            entity.CustomerUserId,
            cancellationToken);
        return (CreateDraftCaseService.ToResponse(entity, customerEmail), Array.Empty<string>(), false, null, null);
    }
}
