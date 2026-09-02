using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kyc.Api.Tests;

/// <summary>
/// Flips the case to InReview during object-storage put so persist-time status recheck is proven (KYC-095).
/// </summary>
public sealed class UploadRaceState
{
    public Guid CaseId { get; set; }
}

public sealed class FlipToInReviewOnPutStorage(
    IObjectStorage inner,
    IServiceScopeFactory scopes,
    UploadRaceState state) : IObjectStorage
{
    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        await inner.PutAsync(key, content, contentType, contentLength, cancellationToken);
        if (state.CaseId == Guid.Empty)
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Cases
            .IgnoreQueryFilters()
            .Where(c => c.Id == state.CaseId && c.Status == CaseStatus.Submitted)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(c => c.Status, CaseStatus.InReview),
                cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        inner.OpenReadAsync(key, cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        inner.DeleteAsync(key, cancellationToken);
}

public sealed class UploadRaceFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<UploadRaceState>();
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<InMemoryObjectStorage>();
            services.AddSingleton<IObjectStorage>(sp => new FlipToInReviewOnPutStorage(
                sp.GetRequiredService<InMemoryObjectStorage>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<UploadRaceState>()));
        });
    }
}

public sealed class UploadDocumentRaceTests(UploadRaceFactory factory) : IClassFixture<UploadRaceFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;
    private Guid _submittedCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _submittedCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Race Co",
            Slug = $"race-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "c@race.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = now
        });
        db.Cases.Add(new Case
        {
            Id = _submittedCaseId,
            TenantId = _tenantId,
            CustomerUserId = _customerId,
            Title = "Submitted",
            Status = CaseStatus.Submitted,
            FormData = "{}",
            CreatedAt = now,
            UpdatedAt = now,
            SubmittedAt = now
        });
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<UploadRaceState>().CaseId = _submittedCaseId;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Persist_recheck_rejects_upload_after_status_leaves_submitted()
    {
        using var jwtScope = factory.Services.CreateScope();
        var jwt = jwtScope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "c@race.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 race");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdf);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "race.pdf");

        using var response = await _client.PostAsync($"/api/cases/{_submittedCaseId}/documents", content);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("DOMAIN", text, StringComparison.Ordinal);
        Assert.Contains(UploadDocumentService.NotUploadableMessage, text, StringComparison.Ordinal);

        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Documents.IgnoreQueryFilters().CountAsync(d => d.CaseId == _submittedCaseId));
        var storage = verify.ServiceProvider.GetRequiredService<InMemoryObjectStorage>();
        Assert.Equal(0, storage.ObjectCount);
    }
}
