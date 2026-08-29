using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Documents;

/// <summary>
/// List document metadata for a case (KYC-041). Same visibility as <c>case</c> / <c>cases</c>; never returns bytes or storage keys.
/// </summary>
public sealed class ListDocumentsService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    public async Task<(IReadOnlyList<CaseDocumentMetadataResponse>? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> ListAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return (null, ["Case id is required."], false, null, null);
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

        var caseExists = await CaseVisibility
            .ApplyRoleFilter(db.Cases.AsNoTracking(), role, userId)
            .AnyAsync(c => c.Id == caseId, cancellationToken);

        if (!caseExists)
        {
            return (null, Array.Empty<string>(), false, "NOT_FOUND", CaseVisibility.NotFoundMessage);
        }

        return (await LoadMetadataAsync(db, caseId, cancellationToken), Array.Empty<string>(), false, null, null);
    }

    /// <summary>
    /// Metadata for a case already known to be visible (used by case detail). Newest first; ordered in memory for SQLite.
    /// </summary>
    internal static async Task<IReadOnlyList<CaseDocumentMetadataResponse>> LoadMetadataAsync(
        AppDbContext db,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var documents = await db.Documents
            .AsNoTracking()
            .Where(d => d.CaseId == caseId)
            .Select(d => new CaseDocumentMetadataResponse(
                d.Id,
                d.FileName,
                d.ContentType,
                d.SizeBytes,
                d.UploadedAt,
                d.UploadedByUserId))
            .ToListAsync(cancellationToken);

        return [.. documents
            .OrderByDescending(d => d.UploadedAt)
            .ThenByDescending(d => d.Id)];
    }
}
