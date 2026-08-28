using System.Globalization;
using System.Text;
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
    public const int MaxFormDataUtf8Bytes = 64 * 1024;
    public const int MaxFormDataDepth = 8;
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
            entity.UpdatedAt,
            entity.SubmittedAt);
}

/// <summary>Shared title / FormData rules for draft create and update.</summary>
internal static class CaseDraftValidation
{
    private static readonly string[] SubmitRequiredFields =
        ["fullName", "dateOfBirth", "nationality", "address"];

    private static readonly JsonDocumentOptions FormDataParseOptions = new()
    {
        MaxDepth = CreateDraftCaseService.MaxFormDataDepth
    };

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

        if (!string.IsNullOrWhiteSpace(formData))
        {
            errors.AddRange(ValidateFormDataDocument(formData));
        }

        return errors;
    }

    /// <summary>
    /// MVP submit rules (KYC-033): required person fields in FormData JSON; company fields optional.
    /// </summary>
    public static List<string> ValidateSubmitFormData(string formData)
    {
        var documentErrors = ValidateFormDataDocument(formData);
        if (documentErrors.Count > 0)
        {
            return documentErrors;
        }

        var errors = new List<string>();

        using var document = JsonDocument.Parse(formData, FormDataParseOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return ["FormData must be a JSON object."];
        }

        foreach (var field in SubmitRequiredFields)
        {
            if (!TryGetNonEmptyString(document.RootElement, field, out var value))
            {
                errors.Add($"{field} is required.");
                continue;
            }

            if (field == "dateOfBirth" && !IsIsoDate(value))
            {
                errors.Add("dateOfBirth must be an ISO date (YYYY-MM-DD).");
            }
        }

        return errors;
    }

    public static string NormalizeFormData(string? formData) =>
        string.IsNullOrWhiteSpace(formData) ? CreateDraftCaseService.EmptyFormData : formData.Trim();

    private static List<string> ValidateFormDataDocument(string formData)
    {
        var trimmed = formData.Trim();
        if (Encoding.UTF8.GetByteCount(trimmed) > CreateDraftCaseService.MaxFormDataUtf8Bytes)
        {
            return [$"FormData must be at most {CreateDraftCaseService.MaxFormDataUtf8Bytes} bytes."];
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed, FormDataParseOptions);
            if (document.RootElement.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array)
            {
                return ["FormData must be valid JSON when provided."];
            }
        }
        catch (JsonException)
        {
            return ["FormData must be valid JSON when provided."];
        }

        return [];
    }

    private static bool TryGetNonEmptyString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool IsIsoDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
