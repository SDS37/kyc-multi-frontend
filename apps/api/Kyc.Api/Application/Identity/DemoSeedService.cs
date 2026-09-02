using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Documents;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Documents;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Identity;

/// <summary>
/// Idempotent Development seed (KYC-101): two tenants, every role, one case per status,
/// plus a tiny PNG on non-draft cases. Uses <c>IgnoreQueryFilters</c> because startup has no JWT.
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

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            LogSeedSkippedUnreachable(logger);
            return;
        }

        try
        {
            _ = await db.Tenants.AsNoTracking().AnyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogSeedSkippedSchema(logger, ex.GetType().Name);
            return;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var spec in DemoSeedCatalog.Tenants)
            {
                await SeedTenantAsync(spec, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        LogSeedCompleted(logger);
    }

    private async Task SeedTenantAsync(DemoTenantSpec spec, CancellationToken cancellationToken)
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
        }

        await EnsureUserAsync(tenant.Id, spec.AdminEmail, UserRole.TenantAdmin, now, cancellationToken);
        var reviewer = await EnsureUserAsync(tenant.Id, spec.ReviewerEmail, UserRole.Reviewer, now, cancellationToken);
        var customer = await EnsureUserAsync(tenant.Id, spec.CustomerEmail, UserRole.Customer, now, cancellationToken);

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

        await EnsureDocumentAsync(submitted, customer.Id, cancellationToken);
        await EnsureDocumentAsync(inReview, customer.Id, cancellationToken);
        await EnsureDocumentAsync(approved, customer.Id, cancellationToken);
        await EnsureDocumentAsync(rejected, customer.Id, cancellationToken);
    }

    private async Task<User> EnsureUserAsync(
        Guid tenantId,
        string email,
        UserRole role,
        DateTimeOffset createdAt,
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
        return entity;
    }

    private async Task EnsureDocumentAsync(Case caseEntity, Guid uploadedByUserId, CancellationToken cancellationToken)
    {
        var hasDocument = await db.Documents
            .IgnoreQueryFilters()
            .AnyAsync(d => d.CaseId == caseEntity.Id, cancellationToken);
        if (hasDocument)
        {
            return;
        }

        var documentId = Guid.NewGuid();
        var fileName = "seed-id.png";
        var storageKey = DocumentUploadValidation.BuildStorageKey(
            caseEntity.TenantId,
            caseEntity.Id,
            documentId,
            fileName);
        await using var content = new MemoryStream(DemoPng, writable: false);
        try
        {
            await objectStorage.PutAsync(
                storageKey,
                content,
                "image/png",
                DemoPng.LongLength,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogSeedDocumentStorageFailed(logger, ex.GetType().Name, caseEntity.Id);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo seed completed (KYC-101).")]
    private static partial void LogSeedCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo seed skipped: database is unreachable.")]
    private static partial void LogSeedSkippedUnreachable(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Demo seed skipped: schema is missing ({ExceptionType}). Apply migrations first.")]
    private static partial void LogSeedSkippedSchema(ILogger logger, string exceptionType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Demo seed skipped document for case {CaseId}: object storage failed ({ExceptionType}).")]
    private static partial void LogSeedDocumentStorageFailed(ILogger logger, string exceptionType, Guid caseId);
}
