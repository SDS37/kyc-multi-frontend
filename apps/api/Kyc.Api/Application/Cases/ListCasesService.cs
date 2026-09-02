using FluentValidation;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Application.Validation;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class ListCasesService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IValidator<ListCasesRequest> validator)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public async Task<(CaseListResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized)> ListAsync(
        ListCasesRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = RequestValidation.Errors(validator, request);
        if (errors.Count > 0)
        {
            return (null, errors, false);
        }

        var skip = request.Skip ?? 0;
        var take = request.Take ?? DefaultPageSize;

        var (userId, role, unauthorized) = await CaseVisibility.ResolveCallerAsync(
            db,
            currentTenant,
            currentUser,
            cancellationToken);

        if (unauthorized)
        {
            return (null, Array.Empty<string>(), true);
        }

        // Tenant filter (KYC-014 / ADR-007) already scopes to JWT tenant.
        IQueryable<Case> query = CaseVisibility.ApplyRoleFilter(db.Cases.AsNoTracking(), role, userId);

        if (request.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        // Stable key only: SQLite tests cannot ORDER BY DateTimeOffset; Id works on all providers.
        // Project without FormData so list pages do not pull large JSON payloads.
        var items = await query
            .OrderByDescending(c => c.Id)
            .Skip(skip)
            .Take(take)
            .Select(c => new CaseListItemResponse(
                c.Id,
                c.Title,
                c.Status,
                c.TenantId,
                c.CustomerUserId,
                c.CustomerUser.Email,
                c.CreatedAt,
                c.UpdatedAt,
                c.SubmittedAt,
                c.ReviewedAt,
                c.ReviewedBy,
                c.ReviewComment))
            .ToListAsync(cancellationToken);

        return (new CaseListResponse(items, totalCount, skip, take), Array.Empty<string>(), false);
    }
}
