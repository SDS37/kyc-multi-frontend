using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class CompleteCaseReviewService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    public const int MaxCommentLength = 2000;
    public const string NotFoundMessage = "Case was not found.";
    public const string NotInReviewMessage = "Only cases in review can be approved or rejected.";
    public const string RejectCommentRequiredMessage = "A comment is required when rejecting a case.";

    public Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> ApproveAsync(
        ApproveCaseRequest request,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            request.Id,
            CaseStatus.Approved,
            request.Comment,
            commentRequired: false,
            cancellationToken);

    public Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> RejectAsync(
        RejectCaseRequest request,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            request.Id,
            CaseStatus.Rejected,
            request.Comment,
            commentRequired: true,
            cancellationToken);

    private async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> CompleteAsync(
        Guid caseId,
        CaseStatus targetStatus,
        string? comment,
        bool commentRequired,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            return (null, ["Case id is required."], false, null, null);
        }

        var tenantId = currentTenant.TenantId;
        var reviewerUserId = currentUser.UserId;
        if (tenantId is null || reviewerUserId is null)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        var reviewerExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == reviewerUserId && u.TenantId == tenantId,
                cancellationToken);

        if (!reviewerExists)
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

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (commentRequired && normalizedComment is null)
        {
            return (null, [RejectCommentRequiredMessage], false, null, null);
        }

        if (normalizedComment is { Length: > MaxCommentLength })
        {
            return (null, [$"Comment must be at most {MaxCommentLength} characters."], false, null, null);
        }

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
