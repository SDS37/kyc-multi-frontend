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

    /// <summary>Generic message for missing claims or stale JWT subject (do not leak existence details).</summary>
    public const string GenericAuthFailure = "Authentication failed.";

    public async Task<(CreateDraftCaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized)> CreateAsync(
        CreateDraftCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return (null, validationErrors, false);
        }

        var tenantId = currentTenant.TenantId;
        var customerUserId = currentUser.UserId;
        if (tenantId is null || customerUserId is null)
        {
            return (null, Array.Empty<string>(), true);
        }

        // Ensure the JWT subject is a real user in this tenant (FK + tenant consistency).
        var customerExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == customerUserId && u.TenantId == tenantId,
                cancellationToken);

        if (!customerExists)
        {
            return (null, Array.Empty<string>(), true);
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
            Array.Empty<string>(),
            false);
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
