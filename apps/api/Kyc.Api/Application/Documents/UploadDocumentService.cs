using FluentValidation;
using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Application.Validation;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Documents;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Documents;

public sealed partial class UploadDocumentService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IObjectStorage objectStorage,
    ILogger<UploadDocumentService> logger,
    IValidator<CaseIdInput> caseIdValidator)
{
    public const string NotFoundMessage = "Case was not found.";
    public const string NotUploadableMessage = "Documents can only be uploaded to draft or submitted cases.";
    public const string ForbiddenMessage = "Only customers can upload documents.";

    public async Task<(CaseDocumentMetadataResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, bool Forbidden, string? ErrorCode, string? ErrorMessage)> UploadAsync(
        Guid caseId,
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        var idErrors = RequestValidation.Errors(caseIdValidator, new CaseIdInput(caseId));
        if (idErrors.Count > 0)
        {
            return (null, idErrors, false, false, null, null);
        }

        if (file is null || file.Length <= 0)
        {
            return (null, ["A non-empty file is required."], false, false, null, null);
        }

        if (file.Length > DocumentUploadValidation.MaxFileBytes)
        {
            return (null, [$"File must be at most {DocumentUploadValidation.MaxFileBytes / (1024 * 1024)} MB."], false, false, null, null);
        }

        var safeName = DocumentUploadValidation.SanitizeFileName(file.FileName);
        if (safeName is null)
        {
            return (null, ["A valid file name is required."], false, false, null, null);
        }

        var contentType = DocumentUploadValidation.NormalizeContentType(file.ContentType);
        if (contentType is null)
        {
            return (null, ["File type must be PDF, PNG, or JPG."], false, false, null, null);
        }

        var tenantId = currentTenant.TenantId;
        var userId = currentUser.UserId;
        if (tenantId is null || userId is null)
        {
            return (null, Array.Empty<string>(), true, false, null, null);
        }

        var allowed = await CallerAuthorization.EnsureUserWithRolesAsync(
            db,
            tenantId.Value,
            userId.Value,
            currentUser.Role,
            [UserRole.Customer],
            cancellationToken);
        if (!allowed)
        {
            if (currentUser.Role is not null and not UserRole.Customer)
            {
                return (null, Array.Empty<string>(), false, true, null, null);
            }

            return (null, Array.Empty<string>(), true, false, null, null);
        }

        var entity = await db.Cases
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (entity is null || entity.CustomerUserId != userId.Value)
        {
            return (null, Array.Empty<string>(), false, false, "NOT_FOUND", NotFoundMessage);
        }

        if (entity.Status is not (CaseStatus.Draft or CaseStatus.Submitted))
        {
            return (null, Array.Empty<string>(), false, false, "DOMAIN", NotUploadableMessage);
        }

        await using var readStream = file.OpenReadStream();
        var header = new byte[8];
        var headerRead = await readStream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!DocumentUploadValidation.MatchesMagicBytes(contentType, header.AsSpan(0, headerRead)))
        {
            return (null, ["File contents do not match the declared type (PDF, PNG, or JPG)."], false, false, null, null);
        }

        if (readStream.CanSeek)
        {
            readStream.Position = 0;
        }
        else
        {
            // Non-seekable: rebuild stream with header + remainder.
            var buffer = new MemoryStream();
            await buffer.WriteAsync(header.AsMemory(0, headerRead), cancellationToken);
            await readStream.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > DocumentUploadValidation.MaxFileBytes)
            {
                return (null, [$"File must be at most {DocumentUploadValidation.MaxFileBytes / (1024 * 1024)} MB."], false, false, null, null);
            }

            buffer.Position = 0;
            return await PersistAsync(
                entity.Id,
                tenantId.Value,
                userId.Value,
                safeName,
                contentType,
                buffer.Length,
                buffer,
                cancellationToken);
        }

        return await PersistAsync(
            entity.Id,
            tenantId.Value,
            userId.Value,
            safeName,
            contentType,
            file.Length,
            readStream,
            cancellationToken);
    }

    private async Task<(CaseDocumentMetadataResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, bool Forbidden, string? ErrorCode, string? ErrorMessage)> PersistAsync(
        Guid caseId,
        Guid tenantId,
        Guid userId,
        string safeName,
        string contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken)
    {
        var documentId = Guid.NewGuid();
        var storageKey = DocumentUploadValidation.BuildStorageKey(tenantId, caseId, documentId, safeName);
        var uploadedAt = DateTimeOffset.UtcNow;

        try
        {
            await objectStorage.PutAsync(storageKey, content, contentType, sizeBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            LogObjectStoragePutFailed(logger, ex, caseId);
            return (
                null,
                Array.Empty<string>(),
                false,
                false,
                "STORAGE",
                "Could not store the document. Please try again.");
        }

        var document = new Document
        {
            Id = documentId,
            TenantId = tenantId,
            CaseId = caseId,
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            UploadedByUserId = userId,
            UploadedAt = uploadedAt
        };

        try
        {
            var persisted = await TryPersistMetadataAsync(
                document,
                tenantId,
                userId,
                uploadedAt,
                cancellationToken);
            if (!persisted)
            {
                await CompensateObjectAsync(storageKey, documentId, caseId, cancellationToken);
                return await UploadRejectedAfterRaceAsync(caseId, userId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            LogDocumentMetadataSaveFailed(logger, ex, documentId);
            await CompensateObjectAsync(storageKey, documentId, caseId, cancellationToken);
            return (null, ["Could not save document metadata. Please try again."], false, false, null, null);
        }

        LogDocumentUploaded(logger, documentId, caseId, sizeBytes, contentType);

        return (
            new CaseDocumentMetadataResponse(
                document.Id,
                document.FileName,
                document.ContentType,
                document.SizeBytes,
                document.UploadedAt,
                document.UploadedByUserId),
            Array.Empty<string>(),
            false,
            false,
            null,
            null);
    }

    /// <summary>
    /// Inserts metadata only if the case is still Draft or Submitted (same-transaction status check).
    /// </summary>
    private async Task<bool> TryPersistMetadataAsync(
        Document document,
        Guid tenantId,
        Guid userId,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var rows = await db.Cases
                .Where(c =>
                    c.Id == document.CaseId &&
                    c.CustomerUserId == userId &&
                    (c.Status == CaseStatus.Draft || c.Status == CaseStatus.Submitted))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(c => c.UpdatedAt, uploadedAt),
                    cancellationToken);
            if (rows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            db.Documents.Add(document);
            AuditRecorder.Append(
                db,
                tenantId,
                userId,
                AuditEntityTypes.Document,
                document.Id,
                AuditActions.DocumentUploaded,
                uploadedAt,
                payload: System.Text.Json.JsonSerializer.Serialize(new
                {
                    caseId = document.CaseId,
                    fileName = document.FileName,
                    contentType = document.ContentType,
                    sizeBytes = document.SizeBytes
                }));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    private async Task<(CaseDocumentMetadataResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, bool Forbidden, string? ErrorCode, string? ErrorMessage)> UploadRejectedAfterRaceAsync(
        Guid caseId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var current = await db.Cases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);
        if (current is null || current.CustomerUserId != userId)
        {
            return (null, Array.Empty<string>(), false, false, "NOT_FOUND", NotFoundMessage);
        }

        return (null, Array.Empty<string>(), false, false, "DOMAIN", NotUploadableMessage);
    }

    private async Task CompensateObjectAsync(
        string storageKey,
        Guid documentId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            await objectStorage.DeleteAsync(storageKey, cancellationToken);
        }
        catch (Exception deleteEx)
        {
            LogDocumentOrphanCleanupFailed(logger, deleteEx, documentId, caseId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Object storage put failed for case {CaseId}")]
    private static partial void LogObjectStoragePutFailed(ILogger logger, Exception ex, Guid caseId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Document metadata save failed for {DocumentId}; compensating delete")]
    private static partial void LogDocumentMetadataSaveFailed(ILogger logger, Exception ex, Guid documentId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Compensating object-storage delete failed for document {DocumentId} case {CaseId}; object may be orphaned")]
    private static partial void LogDocumentOrphanCleanupFailed(
        ILogger logger,
        Exception ex,
        Guid documentId,
        Guid caseId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Document uploaded {DocumentId} case {CaseId} size {SizeBytes} type {ContentType}")]
    private static partial void LogDocumentUploaded(
        ILogger logger,
        Guid documentId,
        Guid caseId,
        long sizeBytes,
        string contentType);
}
