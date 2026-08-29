using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class ListCasesService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public async Task<(CaseListResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized)> ListAsync(
        ListCasesRequest request,
        CancellationToken cancellationToken = default)
    {
        var skip = request.Skip ?? 0;
        var take = request.Take ?? DefaultPageSize;

        var errors = new List<string>();
        if (skip < 0)
        {
            errors.Add("Skip must be zero or greater.");
        }

        if (take < 1 || take > MaxPageSize)
        {
            errors.Add($"Take must be between 1 and {MaxPageSize}.");
        }

        if (errors.Count > 0)
        {
            return (null, errors, false);
        }

        var tenantId = currentTenant.TenantId;
        var userId = currentUser.UserId;
        var role = currentUser.Role;
        if (tenantId is null || userId is null || role is null)
        {
            return (null, Array.Empty<string>(), true);
        }

        var userExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == userId && u.TenantId == tenantId,
                cancellationToken);

        if (!userExists)
        {
            return (null, Array.Empty<string>(), true);
        }

        // Tenant filter (KYC-014 / ADR-007) already scopes to JWT tenant.
        IQueryable<Case> query = db.Cases.AsNoTracking();

        switch (role.Value)
        {
            case UserRole.Customer:
                query = query.Where(c => c.CustomerUserId == userId.Value);
                break;
            case UserRole.Reviewer:
            case UserRole.TenantAdmin:
                break;
            default:
                return (null, Array.Empty<string>(), true);
        }

        if (request.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        // Stable key only: SQLite tests cannot ORDER BY DateTimeOffset; Id works on all providers.
        var entities = await query
            .OrderByDescending(c => c.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);



        var items = entities.Select(CreateDraftCaseService.ToResponse).ToList();
        return (new CaseListResponse(items, totalCount, skip, take), Array.Empty<string>(), false);
    }
}
