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

    public async Task<(CaseResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized)> CreateAsync(
        CreateDraftCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = CaseDraftValidation.ValidateTitleAndFormData(request.Title, request.FormData);
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
        var formData = CaseDraftValidation.NormalizeFormData(request.FormData);

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

        return (ToResponse(entity), Array.Empty<string>(), false);
    }

    internal static CaseResponse ToResponse(Case entity) =>
        new(
            entity.Id,
            entity.Title,
            entity.Status,
            entity.FormData,
            entity.TenantId,
            entity.CustomerUserId,
            entity.CreatedAt,
            entity.UpdatedAt);
}

/// <summary>Shared title / FormData rules for draft create and update.</summary>
internal static class CaseDraftValidation
{
    public static List<string> ValidateTitleAndFormData(string title, string? formData)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("Title is required.");
        }
        else if (title.Trim().Length > CreateDraftCaseService.MaxTitleLength)
        {
            errors.Add($"Title must be at most {CreateDraftCaseService.MaxTitleLength} characters.");
        }

        if (!string.IsNullOrWhiteSpace(formData) && !IsValidJson(formData))
        {
            errors.Add("FormData must be valid JSON when provided.");
        }

        return errors;
    }

    public static string NormalizeFormData(string? formData) =>
        string.IsNullOrWhiteSpace(formData) ? CreateDraftCaseService.EmptyFormData : formData.Trim();

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
