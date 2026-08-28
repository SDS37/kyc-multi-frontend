using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class StartCaseReviewService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    public const string NotFoundMessage = "Case was not found.";
    public const string NotSubmittedMessage = "Only submitted cases can be moved to in review.";

    public async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> StartAsync(
        StartCaseReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id == Guid.Empty)
        {
            return (null, ["Case id is required."], false, null, null);
        }

        var tenantId = currentTenant.TenantId;
        var reviewerUserId = currentUser.UserId;
        if (tenantId is null || reviewerUserId is null)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        // Stale-token guard + FK safety for ReviewedBy.
        var reviewerExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == reviewerUserId && u.TenantId == tenantId,
                cancellationToken);

        if (!reviewerExists)
        {
            return (null, Array.Empty<string>(), true, null, null);
        }

        // Tenant filter enforces same-tenant only (KYC-014 / ADR-007).
        var entity = await db.Cases
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
        }

        if (entity.Status != CaseStatus.Submitted)
        {
            return (null, Array.Empty<string>(), false, "DOMAIN", NotSubmittedMessage);
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = CaseStatus.InReview;
        entity.ReviewedBy = reviewerUserId.Value;
        entity.UpdatedAt = now;
        // ReviewedAt is set on approve/reject, not when review starts.
        await db.SaveChangesAsync(cancellationToken);

        return (CreateDraftCaseService.ToResponse(entity), Array.Empty<string>(), false, null, null);
    }
}
