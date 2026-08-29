using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Documents;
using Kyc.Api.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class DownloadDocumentTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static readonly byte[] PdfBytes = "%PDF-1.4 download-fixture"u8.ToArray();

    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _customerId;
    private Guid _peerCustomerId;
    private Guid _reviewerId;
    private Guid _adminId;
    private Guid _otherTenantCustomerId;
    private Guid _ownerCaseId;
    private Guid _peerCaseId;
    private Guid _otherTenantCaseId;
    private Guid _documentId;
    private Guid _missingBlobDocumentId;
    private string _storageKey = null!;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _otherTenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _peerCustomerId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _adminId = Guid.NewGuid();
        _otherTenantCustomerId = Guid.NewGuid();
        _ownerCaseId = Guid.NewGuid();
        _peerCaseId = Guid.NewGuid();
        _otherTenantCaseId = Guid.NewGuid();
        _documentId = Guid.NewGuid();
        _missingBlobDocumentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _storageKey = DocumentUploadValidation.BuildStorageKey(
            _tenantId,
            _ownerCaseId,
            _documentId,
            "id.pdf");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "Download Docs Co",
                Slug = $"ddc-{_tenantId:N}"[..20],
                IsActive = true,
                CreatedAt = now
            },
            new Tenant
            {
                Id = _otherTenantId,
                Name = "Other",
                Slug = $"oth-{_otherTenantId:N}"[..20],
                IsActive = true,
                CreatedAt = now
            });
        db.Users.AddRange(
            new User
            {
                Id = _customerId,
                TenantId = _tenantId,
                Email = "customer@downloaddocs.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _peerCustomerId,
                TenantId = _tenantId,
                Email = "peer@downloaddocs.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@downloaddocs.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = _adminId,
                TenantId = _tenantId,
                Email = "admin@downloaddocs.example",
                PasswordHash = "unused",
                Role = UserRole.TenantAdmin,
                CreatedAt = now
            },
            new User
            {
                Id = _otherTenantCustomerId,
                TenantId = _otherTenantId,
                Email = "customer@oth.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            });
        db.Cases.AddRange(
            new Case
            {
                Id = _ownerCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "Owner",
                Status = CaseStatus.Submitted,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now
            },
            new Case
            {
                Id = _peerCaseId,
                TenantId = _tenantId,
                CustomerUserId = _peerCustomerId,
                Title = "Peer",
                Status = CaseStatus.Draft,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Case
            {
                Id = _otherTenantCaseId,
                TenantId = _otherTenantId,
                CustomerUserId = _otherTenantCustomerId,
                Title = "Other",
                Status = CaseStatus.Draft,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now
            });
        db.Documents.AddRange(
            new Document
            {
                Id = _documentId,
                TenantId = _tenantId,
                CaseId = _ownerCaseId,
                FileName = "id.pdf",
                ContentType = "application/pdf",
                SizeBytes = PdfBytes.Length,
                StorageKey = _storageKey,
                UploadedByUserId = _customerId,
                UploadedAt = now
            },
            new Document
            {
                Id = _missingBlobDocumentId,
                TenantId = _tenantId,
                CaseId = _ownerCaseId,
                FileName = "orphan.pdf",
                ContentType = "application/pdf",
                SizeBytes = 4,
                StorageKey = DocumentUploadValidation.BuildStorageKey(
                    _tenantId,
                    _ownerCaseId,
                    _missingBlobDocumentId,
                    "orphan.pdf"),
                UploadedByUserId = _customerId,
                UploadedAt = now
            });
        await db.SaveChangesAsync();

        await using var put = new MemoryStream(PdfBytes);
        await storage.PutAsync(_storageKey, put, "application/pdf", PdfBytes.Length);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Owner_downloads_own_document_bytes()
    {
        Authenticate(UserRole.Customer, _customerId);
        using var response = await _client.GetAsync(DownloadPath(_ownerCaseId, _documentId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("id.pdf", response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(PdfBytes, bytes);

        var bodyText = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("storageKey", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_storageKey, bodyText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reviewer_downloads_tenant_document()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);
        using var response = await _client.GetAsync(DownloadPath(_ownerCaseId, _documentId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task TenantAdmin_downloads_tenant_document()
    {
        Authenticate(UserRole.TenantAdmin, _adminId);
        using var response = await _client.GetAsync(DownloadPath(_ownerCaseId, _documentId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Peer_customer_gets_NOT_FOUND()
    {
        Authenticate(UserRole.Customer, _peerCustomerId);
        using var response = await _client.GetAsync(DownloadPath(_ownerCaseId, _documentId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertJsonCodeAsync(response, "NOT_FOUND");
    }

    [Fact]
    public async Task Other_tenant_case_is_NOT_FOUND()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);
        using var response = await _client.GetAsync(DownloadPath(_otherTenantCaseId, _documentId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertJsonCodeAsync(response, "NOT_FOUND");
    }

    [Fact]
    public async Task Wrong_case_id_for_document_is_NOT_FOUND()
    {
        Authenticate(UserRole.Customer, _customerId);
        using var response = await _client.GetAsync(DownloadPath(_peerCaseId, _documentId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertJsonCodeAsync(response, "NOT_FOUND");
    }

    [Fact]
    public async Task Missing_object_blob_is_NOT_FOUND()
    {
        Authenticate(UserRole.Customer, _customerId);
        using var response = await _client.GetAsync(DownloadPath(_ownerCaseId, _missingBlobDocumentId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertJsonCodeAsync(response, "NOT_FOUND");
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        using var response = await _client.GetAsync(DownloadPath(_ownerCaseId, _documentId));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Response_never_redirects_to_object_storage()
    {
        Authenticate(UserRole.Customer, _customerId);
        using var response = await _client.GetAsync(DownloadPath(_ownerCaseId, _documentId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("127.0.0.1:9000", content, StringComparison.Ordinal);
        Assert.DoesNotContain("minio", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Amz-", content, StringComparison.OrdinalIgnoreCase);
    }

    private static string DownloadPath(Guid caseId, Guid documentId) =>
        $"/api/cases/{caseId}/documents/{documentId}";

    private static async Task AssertJsonCodeAsync(HttpResponseMessage response, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }

    private void Authenticate(UserRole role, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = userId,
            TenantId = _tenantId,
            Email = $"{role.ToString().ToLowerInvariant()}@downloaddocs.example",
            PasswordHash = "unused",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
