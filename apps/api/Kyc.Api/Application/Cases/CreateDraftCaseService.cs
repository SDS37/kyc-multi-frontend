using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Cases;

public sealed class CreateDraftCaseService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    public const int MaxTitleLength = 200;
    public const string EmptyFormData = "{}";

    public async Task<(CreateDraftCaseResponse? Result, IReadOnlyList<string> Errors)> CreateAsync(
        CreateDraftCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var tenantId = currentTenant.TenantId;
        var customerUserId = currentUser.UserId;
        if (tenantId is null || customerUserId is null)
        {
            return (null, ["Authentication context is incomplete."]);
        }

        // Ensure the JWT subject is a real user in this tenant (FK + tenant consistency).
        var customerExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == customerUserId && u.TenantId == tenantId,
                cancellationToken);

        if (!customerExists)
        {
            return (null, ["Customer user was not found for this tenant."]);
        }

        var now = DateTimeOffset.UtcNow;
        var formData = NormalizeFormData(request.FormData);

        var entity = new Case
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            CustomerUserId = customerUserId.Value,
            Title = request.Title.Trim(),
            Status = CaseStatus.Draft,
            FormData = formData,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Cases.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return (
            new CreateDraftCaseResponse(
                entity.Id,
                entity.Title,
                entity.Status,
                entity.FormData,
                entity.TenantId,
                entity.CustomerUserId,
                entity.CreatedAt,
                entity.UpdatedAt),
            Array.Empty<string>());
    }

    private static List<string> Validate(CreateDraftCaseRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add("Title is required.");
        }
        else if (request.Title.Trim().Length > MaxTitleLength)
        {
            errors.Add($"Title must be at most {MaxTitleLength} characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.FormData) && !IsValidJson(request.FormData))
        {
            errors.Add("FormData must be valid JSON when provided.");
        }

        return errors;
    }

    private static string NormalizeFormData(string? formData) =>
        string.IsNullOrWhiteSpace(formData) ? EmptyFormData : formData.Trim();

    private static bool IsValidJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
