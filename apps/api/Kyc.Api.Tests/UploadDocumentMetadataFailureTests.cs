using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Documents;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kyc.Api.Tests;

/// <summary>Arms a metadata-save failure after a successful object put (KYC-113).</summary>
public sealed class DocumentInsertFailState
{
    public bool FailDocumentSave { get; set; }
}

public sealed class DocumentInsertFailInterceptor(DocumentInsertFailState state) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfDocumentInsert(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDocumentInsert(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ThrowIfDocumentInsert(DbContextEventData eventData)
    {
        if (!state.FailDocumentSave || eventData.Context is null)
        {
            return;
        }

        var addingDocument = eventData.Context.ChangeTracker
            .Entries<Document>()
            .Any(entry => entry.State == EntityState.Added);
        if (!addingDocument)
        {
            return;
        }

        throw new InvalidOperationException("metadata save failed");
    }
}

/// <summary>In-memory store that can fail the compensating delete without putting the key in the exception.</summary>
public sealed class TrackingObjectStorage(InMemoryObjectStorage inner) : IObjectStorage
{
    public bool ThrowOnDelete { get; set; }

    public int DeleteCount { get; private set; }

    public Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default) =>
        inner.PutAsync(key, content, contentType, contentLength, cancellationToken);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        inner.OpenReadAsync(key, cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        DeleteCount++;
        if (ThrowOnDelete)
        {
            throw new InvalidOperationException("compensating delete failed");
        }

        return inner.DeleteAsync(key, cancellationToken);
    }

    public int ObjectCount => inner.ObjectCount;
}

public sealed class MetadataSaveFailFactory : ApiFactory
{
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(Logs);
            logging.SetMinimumLevel(LogLevel.Information);
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<DocumentInsertFailState>();
            services.AddSingleton<DocumentInsertFailInterceptor>();
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<InMemoryObjectStorage>();
            services.AddSingleton<TrackingObjectStorage>();
            services.AddSingleton<IObjectStorage>(sp => sp.GetRequiredService<TrackingObjectStorage>());
        });
    }

    protected override void ConfigureSqlite(IServiceProvider serviceProvider, DbContextOptionsBuilder options)
    {
        options.AddInterceptors(serviceProvider.GetRequiredService<DocumentInsertFailInterceptor>());
    }
}

public sealed class UploadDocumentMetadataFailureTests(MetadataSaveFailFactory factory)
    : IClassFixture<MetadataSaveFailFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;
    private Guid _draftCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _draftCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Meta Fail",
            Slug = $"mdf-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@metafail.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = now
        });
        db.Cases.Add(new Case
        {
            Id = _draftCaseId,
            TenantId = _tenantId,
            CustomerUserId = _customerId,
            Title = "Draft",
            Status = CaseStatus.Draft,
            FormData = "{}",
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        using var jwtScope = factory.Services.CreateScope();
        var jwt = jwtScope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@metafail.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = now
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Metadata_save_failure_after_put_returns_STORAGE_and_deletes_object()
    {
        var state = factory.Services.GetRequiredService<DocumentInsertFailState>();
        var storage = factory.Services.GetRequiredService<TrackingObjectStorage>();
        state.FailDocumentSave = true;
        storage.ThrowOnDelete = false;
        var deletesBefore = storage.DeleteCount;
        var objectsBefore = storage.ObjectCount;

        using var response = await PostPdfAsync();
        var body = await response.Content.ReadAsStringAsync();
        state.FailDocumentSave = false;

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("STORAGE", body, StringComparison.Ordinal);
        Assert.Contains(UploadDocumentService.StorageFailureMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", body, StringComparison.Ordinal);
        Assert.Equal(deletesBefore + 1, storage.DeleteCount);
        Assert.Equal(objectsBefore, storage.ObjectCount);

        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Documents.IgnoreQueryFilters().CountAsync(d => d.CaseId == _draftCaseId));
    }

    [Fact]
    public async Task Failing_compensate_is_logged_at_Error_without_storage_key()
    {
        var state = factory.Services.GetRequiredService<DocumentInsertFailState>();
        var storage = factory.Services.GetRequiredService<TrackingObjectStorage>();
        state.FailDocumentSave = true;
        storage.ThrowOnDelete = true;
        var objectsBefore = storage.ObjectCount;

        using var response = await PostPdfAsync();
        var body = await response.Content.ReadAsStringAsync();
        state.FailDocumentSave = false;
        storage.ThrowOnDelete = false;

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("STORAGE", body, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", body, StringComparison.Ordinal);
        Assert.Equal(objectsBefore + 1, storage.ObjectCount);

        var orphanLog = Assert.Single(
            factory.Logs.Entries,
            entry => entry.Message.Contains("Compensating object-storage delete failed", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, orphanLog.Level);
        Assert.Contains(_draftCaseId.ToString(), orphanLog.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenants/", orphanLog.Message, StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> PostPdfAsync()
    {
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 meta");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdf);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "meta.pdf");
        return await _client.PostAsync($"/api/cases/{_draftCaseId}/documents", content);
    }
}
