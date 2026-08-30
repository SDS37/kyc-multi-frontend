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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kyc.Api.Tests;

public sealed class ThrowingObjectStorage : IObjectStorage
{
    public Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("object storage unavailable");

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class FailingStorageApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage, ThrowingObjectStorage>();
        });
    }
}

public sealed class FailingStorageUploadTests(FailingStorageApiFactory factory)
    : IClassFixture<FailingStorageApiFactory>, IAsyncLifetime
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
            Name = "Fail Store",
            Slug = $"fst-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@fail.example",
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
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Storage_put_failure_returns_STORAGE_not_VALIDATION()
    {
        using var jwtScope = factory.Services.CreateScope();
        var jwt = jwtScope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@fail.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 x");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdf);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "x.pdf");

        using var response = await _client.PostAsync($"/api/cases/{_draftCaseId}/documents", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("STORAGE", body, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", body, StringComparison.Ordinal);
    }
}
