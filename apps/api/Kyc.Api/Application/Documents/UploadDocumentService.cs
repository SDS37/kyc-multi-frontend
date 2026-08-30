using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
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
    ILogger<UploadDocumentService> logger)
{
    public const string NotFoundMessage = "Case was not found.";
    public const string NotUploadableMessage = "Documents can only be uploaded to draft or submitted cases.";
    public const string ForbiddenMessage = "Only customers can upload documents.";

    public async Task<(CaseDocumentMetadataResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, bool Forbidden, string? ErrorCode, string? ErrorMessage)> UploadAsync(
        Guid caseId,
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return (null, ["Case id is required."], false, false, null, null);
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
        var role = currentUser.Role;
        if (tenantId is null || userId is null || role is null)
        {
            return (null, Array.Empty<string>(), true, false, null, null);
        }

        if (role != UserRole.Customer)
        {
            return (null, Array.Empty<string>(), false, true, null, null);
        }

        var userExists = await db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == userId && u.TenantId == tenantId,
                cancellationToken);

        if (!userExists)
        {
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
                entity,
                tenantId.Value,
                userId.Value,
                safeName,
                contentType,
                buffer.Length,
                buffer,
                cancellationToken);
        }

        return await PersistAsync(
            entity,
            tenantId.Value,
            userId.Value,
            safeName,
            contentType,
            file.Length,
            readStream,
            cancellationToken);
    }

    private async Task<(CaseDocumentMetadataResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized, bool Forbidden, string? ErrorCode, string? ErrorMessage)> PersistAsync(
        Case caseEntity,
        Guid tenantId,
        Guid userId,
        string safeName,
        string contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken)
    {
        var documentId = Guid.NewGuid();
        var storageKey = DocumentUploadValidation.BuildStorageKey(tenantId, caseEntity.Id, documentId, safeName);
        var uploadedAt = DateTimeOffset.UtcNow;

        try
        {
            await objectStorage.PutAsync(storageKey, content, contentType, sizeBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            LogObjectStoragePutFailed(logger, ex, caseEntity.Id);
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
            CaseId = caseEntity.Id,
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            UploadedByUserId = userId,
            UploadedAt = uploadedAt
        };

        try
        {
            db.Documents.Add(document);
            caseEntity.UpdatedAt = uploadedAt;
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
                    caseId = caseEntity.Id,
                    fileName = document.FileName,
                    contentType = document.ContentType,
                    sizeBytes = document.SizeBytes
                }));
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogDocumentMetadataSaveFailed(logger, ex, documentId);
            await objectStorage.DeleteAsync(storageKey, cancellationToken);
            return (null, ["Could not save document metadata. Please try again."], false, false, null, null);
        }

        LogDocumentUploaded(logger, documentId, caseEntity.Id, sizeBytes, contentType);

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Object storage put failed for case {CaseId}")]
    private static partial void LogObjectStoragePutFailed(ILogger logger, Exception ex, Guid caseId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Document metadata save failed for {DocumentId}; compensating delete")]
    private static partial void LogDocumentMetadataSaveFailed(ILogger logger, Exception ex, Guid documentId);

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
