using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Documents;

/// <summary>
/// Authenticated document download (KYC-042). Same case visibility as list/detail; streams bytes via REST.
/// Never exposes storage keys or MinIO URLs.
/// </summary>
public sealed partial class DownloadDocumentService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IObjectStorage objectStorage,
    ILogger<DownloadDocumentService> logger)
{
    public const string NotFoundMessage = "Document was not found.";

    public async Task<(DocumentDownloadResult? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, string? ErrorCode, string? ErrorMessage)> DownloadAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return (null, ["Case id is required."], false, null, null);
        }

        if (documentId == Guid.Empty)
        {
            return (null, ["Document id is required."], false, null, null);
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
            // Same as list: hide existence of cases outside visibility.
            return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
        }

        var document = await db.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId && d.CaseId == caseId)
            .Select(d => new { d.StorageKey, d.FileName, d.ContentType, d.SizeBytes })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
        }

        Stream? content;
        try
        {
            content = await objectStorage.OpenReadAsync(document.StorageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            LogObjectStorageReadFailed(logger, ex, documentId);
            return (null, ["Could not read the document. Please try again."], false, null, null);
        }

        if (content is null)
        {
            LogObjectMissing(logger, documentId);
            return (null, Array.Empty<string>(), false, "NOT_FOUND", NotFoundMessage);
        }

        return (
            new DocumentDownloadResult(content, document.FileName, document.ContentType, document.SizeBytes),
            Array.Empty<string>(),
            false,
            null,
            null);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Object storage read failed for document {DocumentId}")]
    private static partial void LogObjectStorageReadFailed(ILogger logger, Exception ex, Guid documentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Document metadata exists but object is missing for {DocumentId}")]
    private static partial void LogObjectMissing(ILogger logger, Guid documentId);
}

/// <summary>Caller must dispose <see cref="Content"/> (ASP.NET <c>Results.File</c> does on success).</summary>
public sealed record DocumentDownloadResult(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);
