using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Documents;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Documents;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kyc.Api.Application.Identity;

/// <summary>
/// Idempotent Development seed (KYC-101): two tenants, every role, one case per status,
/// plus a tiny PNG on non-draft cases. Uses <c>IgnoreQueryFilters</c> because startup has no JWT.
/// Object-storage OpenRead happens after the DB transaction so a down MinIO cannot block
/// tenant/user rows — and Program runs blob repair after the host is listening.
/// </summary>
public sealed partial class DemoSeedService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IObjectStorage objectStorage,
    ILogger<DemoSeedService> logger)
{
    /// <summary>1×1 PNG (valid magic bytes) so Angular/React can download a seed document.</summary>
    private static readonly byte[] DemoPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly TimeSpan StorageBudget = TimeSpan.FromSeconds(3);

    private bool _storageUnavailable;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await TryPrepareAsync(cancellationToken))
        {
            return;
        }

        _storageUnavailable = false;
        var progress = new SeedProgress();
        await ExecuteSeedRowsAsync(progress, cancellationToken);
        await RepairSeedBlobsCoreAsync(progress, cancellationToken);
        LogSeedOutcome(progress);
    }

    /// <summary>
    /// Inserts missing tenants, users, cases, and new <c>seed-id.png</c> rows. Puts blobs only for
    /// newly created documents. Does not OpenRead existing objects (that is <see cref="RepairSeedBlobsAsync"/>).
    /// </summary>
    /// <returns><c>false</c> when the database is unreachable or the schema is missing.</returns>
    public async Task<bool> SeedRowsAsync(CancellationToken cancellationToken = default)
    {
        if (!await TryPrepareAsync(cancellationToken))
        {
            return false;
        }

        _storageUnavailable = false;
        var progress = new SeedProgress();
        await ExecuteSeedRowsAsync(progress, cancellationToken);
        LogSeedOutcome(progress);
        return true;
    }

    /// <summary>
    /// Restores missing object bytes for existing <c>seed-id.png</c> rows only. Stops after the first
    /// storage failure so a down MinIO cannot stall startup or /health.
    /// </summary>
    public async Task RepairSeedBlobsAsync(CancellationToken cancellationToken = default)
    {
        if (!await TryPrepareAsync(cancellationToken))
        {
            return;
        }

        _storageUnavailable = false;
        var progress = new SeedProgress();
        await RepairSeedBlobsCoreAsync(progress, cancellationToken);
        if (progress.Applied)
        {
            LogSeedCompleted(logger);
        }
    }

    private async Task<bool> TryPrepareAsync(CancellationToken cancellationToken)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            LogSeedSkippedUnreachable(logger);
            return false;
        }

        try
        {
            _ = await db.Tenants.AsNoTracking().AnyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogSeedSkippedSchema(logger, ex.GetType().Name);
            return false;
        }

        return true;
    }

    private async Task ExecuteSeedRowsAsync(SeedProgress progress, CancellationToken cancellationToken)
    {
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                progress.Applied = false;
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                foreach (var spec in DemoSeedCatalog.Tenants)
                {
                    await SeedTenantAsync(spec, progress, cancellationToken);
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) || ex is DbUpdateConcurrencyException)
        {
            LogSeedUniqueConflict(logger);
            progress.Applied = false;
        }
    }

    private async Task RepairSeedBlobsCoreAsync(SeedProgress progress, CancellationToken cancellationToken)
    {
        var seedDocuments = await db.Documents
            .IgnoreQueryFilters()
            .Where(d => d.FileName == DemoSeedCatalog.DocumentFileName)
            .ToListAsync(cancellationToken);

        foreach (var document in seedDocuments)
        {
            if (_storageUnavailable)
            {
                break;
            }

            if (await ObjectExistsAsync(document.StorageKey, document.CaseId, cancellationToken))
            {
                continue;
            }

            if (await TryPutDemoPngAsync(document.StorageKey, document.ContentType, document.CaseId, cancellationToken))
            {
                progress.Applied = true;
            }
        }
    }

    private void LogSeedOutcome(SeedProgress progress)
    {
        if (progress.Applied)
        {
            LogSeedCompleted(logger);
        }
        else
        {
            LogSeedUnchanged(logger);
        }
    }

    private async Task SeedTenantAsync(
        DemoTenantSpec spec,
        SeedProgress progress,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == spec.Slug, cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = spec.Name,
                Slug = spec.Slug,
                IsActive = true,
                CreatedAt = now
            };
            db.Tenants.Add(tenant);
            progress.Applied = true;
        }

        await EnsureUserAsync(tenant.Id, spec.AdminEmail, UserRole.TenantAdmin, now, progress, cancellationToken);
        var reviewer = await EnsureUserAsync(
            tenant.Id,
            spec.ReviewerEmail,
            UserRole.Reviewer,
            now,
            progress,
            cancellationToken);
        var customer = await EnsureUserAsync(
            tenant.Id,
            spec.CustomerEmail,
            UserRole.Customer,
            now,
            progress,
            cancellationToken);

        var draft = await EnsureCaseAsync(
            tenant.Id,
            customer.Id,
            DemoSeedCatalog.DraftTitle,
            CaseStatus.Draft,
            "{}",
            createdAt: now.AddMinutes(-40),
            submittedAt: null,
            reviewedAt: null,
            reviewedBy: null,
            reviewComment: null,
            progress,
            cancellationToken);
        var submitted = await EnsureCaseAsync(
            tenant.Id,
            customer.Id,
            DemoSeedCatalog.SubmittedTitle,
            CaseStatus.Submitted,
            DemoSeedCatalog.CompleteFormData,
            createdAt: now.AddMinutes(-35),
            submittedAt: now.AddMinutes(-30),
            reviewedAt: null,
            reviewedBy: null,
            reviewComment: null,
            progress,
            cancellationToken);
        var inReview = await EnsureCaseAsync(
            tenant.Id,
            customer.Id,
            DemoSeedCatalog.InReviewTitle,
            CaseStatus.InReview,
            DemoSeedCatalog.CompleteFormData,
            createdAt: now.AddMinutes(-28),
            submittedAt: now.AddMinutes(-25),
            reviewedAt: null,
            reviewedBy: reviewer.Id,
            reviewComment: null,
            progress,
            cancellationToken);
        var approved = await EnsureCaseAsync(
            tenant.Id,
            customer.Id,
            DemoSeedCatalog.ApprovedTitle,
            CaseStatus.Approved,
            DemoSeedCatalog.CompleteFormData,
            createdAt: now.AddMinutes(-22),
            submittedAt: now.AddMinutes(-18),
            reviewedAt: now.AddMinutes(-10),
            reviewedBy: reviewer.Id,
            reviewComment: DemoSeedCatalog.ApproveComment,
            progress,
            cancellationToken);
        var rejected = await EnsureCaseAsync(
            tenant.Id,
            customer.Id,
            DemoSeedCatalog.RejectedTitle,
            CaseStatus.Rejected,
            DemoSeedCatalog.CompleteFormData,
            createdAt: now.AddMinutes(-16),
            submittedAt: now.AddMinutes(-12),
            reviewedAt: now.AddMinutes(-5),
            reviewedBy: reviewer.Id,
            reviewComment: DemoSeedCatalog.RejectComment,
            progress,
            cancellationToken);

        AppendNewCaseAudit(draft, customer.Id, reviewer.Id, includeSubmit: false, includeReview: false, finishAction: null);
        AppendNewCaseAudit(submitted, customer.Id, reviewer.Id, includeSubmit: true, includeReview: false, finishAction: null);
        AppendNewCaseAudit(inReview, customer.Id, reviewer.Id, includeSubmit: true, includeReview: true, finishAction: null);
        AppendNewCaseAudit(
            approved,
            customer.Id,
            reviewer.Id,
            includeSubmit: true,
            includeReview: true,
            finishAction: AuditActions.CaseApproved);
        AppendNewCaseAudit(
            rejected,
            customer.Id,
            reviewer.Id,
            includeSubmit: true,
            includeReview: true,
            finishAction: AuditActions.CaseRejected);

        await EnsureDocumentAsync(submitted, customer.Id, progress, cancellationToken);
        await EnsureDocumentAsync(inReview, customer.Id, progress, cancellationToken);
        await EnsureDocumentAsync(approved, customer.Id, progress, cancellationToken);
        await EnsureDocumentAsync(rejected, customer.Id, progress, cancellationToken);
    }

    private async Task<User> EnsureUserAsync(
        Guid tenantId,
        string email,
        UserRole role,
        DateTimeOffset createdAt,
        SeedProgress progress,
        CancellationToken cancellationToken)
    {
        var existing = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Role = role,
            CreatedAt = createdAt
        };
        user.PasswordHash = passwordHasher.HashPassword(user, DemoSeedCatalog.Password);
        db.Users.Add(user);
        progress.Applied = true;
        return user;
    }

    private async Task<Case> EnsureCaseAsync(
        Guid tenantId,
        Guid customerUserId,
        string title,
        CaseStatus status,
        string formData,
        DateTimeOffset createdAt,
        DateTimeOffset? submittedAt,
        DateTimeOffset? reviewedAt,
        Guid? reviewedBy,
        string? reviewComment,
        SeedProgress progress,
        CancellationToken cancellationToken)
    {
        var existing = await db.Cases
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Title == title, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var entity = new Case
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerUserId = customerUserId,
            Title = title,
            Status = status,
            FormData = formData,
            CreatedAt = createdAt,
            UpdatedAt = reviewedAt ?? submittedAt ?? createdAt,
            SubmittedAt = submittedAt,
            ReviewedAt = reviewedAt,
            ReviewedBy = reviewedBy,
            ReviewComment = reviewComment
        };
        db.Cases.Add(entity);
        progress.Applied = true;
        return entity;
    }

    private async Task EnsureDocumentAsync(
        Case caseEntity,
        Guid uploadedByUserId,
        SeedProgress progress,
        CancellationToken cancellationToken)
    {
        var existing = await db.Documents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                d => d.CaseId == caseEntity.Id && d.FileName == DemoSeedCatalog.DocumentFileName,
                cancellationToken);
        if (existing is not null)
        {
            return;
        }

        if (_storageUnavailable)
        {
            return;
        }

        var documentId = Guid.NewGuid();
        var fileName = DemoSeedCatalog.DocumentFileName;
        var storageKey = DocumentUploadValidation.BuildStorageKey(
            caseEntity.TenantId,
            caseEntity.Id,
            documentId,
            fileName);
        if (!await TryPutDemoPngAsync(storageKey, "image/png", caseEntity.Id, cancellationToken))
        {
            return;
        }

        var uploadedAt = caseEntity.SubmittedAt ?? caseEntity.CreatedAt;
        db.Documents.Add(new Document
        {
            Id = documentId,
            TenantId = caseEntity.TenantId,
            CaseId = caseEntity.Id,
            FileName = fileName,
            ContentType = "image/png",
            SizeBytes = DemoPng.LongLength,
            StorageKey = storageKey,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = uploadedAt
        });
        progress.Applied = true;
        AuditRecorder.Append(
            db,
            caseEntity.TenantId,
            uploadedByUserId,
            AuditEntityTypes.Document,
            documentId,
            AuditActions.DocumentUploaded,
            uploadedAt,
            payload: """{"fileName":"seed-id.png","contentType":"image/png"}""");
    }

    private async Task<bool> ObjectExistsAsync(string storageKey, Guid caseId, CancellationToken cancellationToken)
    {
        if (_storageUnavailable)
        {
            return false;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(StorageBudget);
        try
        {
            var blob = await objectStorage.OpenReadAsync(storageKey, linked.Token);
            if (blob is null)
            {
                return false;
            }

            await blob.DisposeAsync();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _storageUnavailable = true;
            LogSeedDocumentStorageFailed(logger, "Timeout", caseId);
            return false;
        }
        catch (Exception ex)
        {
            _storageUnavailable = true;
            LogSeedDocumentStorageFailed(logger, ex.GetType().Name, caseId);
            return false;
        }
    }

    private async Task<bool> TryPutDemoPngAsync(
        string storageKey,
        string contentType,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (_storageUnavailable)
        {
            return false;
        }

        await using var content = new MemoryStream(DemoPng, writable: false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(StorageBudget);
        try
        {
            await objectStorage.PutAsync(
                storageKey,
                content,
                contentType,
                DemoPng.LongLength,
                linked.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _storageUnavailable = true;
            LogSeedDocumentStorageFailed(logger, "Timeout", caseId);
            return false;
        }
        catch (Exception ex)
        {
            _storageUnavailable = true;
            LogSeedDocumentStorageFailed(logger, ex.GetType().Name, caseId);
            return false;
        }
    }

    private void AppendNewCaseAudit(
        Case entity,
        Guid customerUserId,
        Guid reviewerUserId,
        bool includeSubmit,
        bool includeReview,
        string? finishAction)
    {
        if (db.Entry(entity).State != EntityState.Added)
        {
            return;
        }

        AuditRecorder.Append(
            db,
            entity.TenantId,
            customerUserId,
            AuditEntityTypes.Case,
            entity.Id,
            AuditActions.CaseCreated,
            entity.CreatedAt);

        if (includeSubmit && entity.SubmittedAt is { } submittedAt)
        {
            AuditRecorder.Append(
                db,
                entity.TenantId,
                customerUserId,
                AuditEntityTypes.Case,
                entity.Id,
                AuditActions.CaseSubmitted,
                submittedAt);
        }

        if (includeReview)
        {
            var reviewStartedAt = entity.SubmittedAt?.AddMinutes(2) ?? entity.CreatedAt;
            AuditRecorder.Append(
                db,
                entity.TenantId,
                reviewerUserId,
                AuditEntityTypes.Case,
                entity.Id,
                AuditActions.ReviewStarted,
                reviewStartedAt);
        }

        if (finishAction is not null && entity.ReviewedAt is { } reviewedAt)
        {
            AuditRecorder.Append(
                db,
                entity.TenantId,
                reviewerUserId,
                AuditEntityTypes.Case,
                entity.Id,
                finishAction,
                reviewedAt);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return false;
        }

        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }

            if (inner.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class SeedProgress
    {
        public bool Applied { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo seed completed (KYC-101).")]
    private static partial void LogSeedCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Demo seed unchanged (KYC-101).")]
    private static partial void LogSeedUnchanged(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo seed skipped: database is unreachable.")]
    private static partial void LogSeedSkippedUnreachable(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Demo seed skipped: schema is missing ({ExceptionType}). Apply migrations first.")]
    private static partial void LogSeedSkippedSchema(ILogger logger, string exceptionType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Demo seed skipped: unique constraint (concurrent start).")]
    private static partial void LogSeedUniqueConflict(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Demo seed skipped document for case {CaseId}: object storage failed ({ExceptionType}).")]
    private static partial void LogSeedDocumentStorageFailed(ILogger logger, string exceptionType, Guid caseId);
}
