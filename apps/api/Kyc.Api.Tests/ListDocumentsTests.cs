using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Documents;
using Kyc.Api.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class ListDocumentsTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
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
    private Guid _docOlderId;
    private Guid _docNewerId;

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
        _docOlderId = Guid.NewGuid();
        _docNewerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "List Docs Co",
                Slug = $"ldc-{_tenantId:N}"[..20],
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
                Email = "customer@listdocs.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _peerCustomerId,
                TenantId = _tenantId,
                Email = "peer@listdocs.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@listdocs.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = _adminId,
                TenantId = _tenantId,
                Email = "admin@listdocs.example",
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
                Id = _docOlderId,
                TenantId = _tenantId,
                CaseId = _ownerCaseId,
                FileName = "older.pdf",
                ContentType = "application/pdf",
                SizeBytes = 10,
                StorageKey = $"tenants/{_tenantId:N}/cases/{_ownerCaseId:N}/{_docOlderId:N}/older.pdf",
                UploadedByUserId = _customerId,
                UploadedAt = now.AddMinutes(-5)
            },
            new Document
            {
                Id = _docNewerId,
                TenantId = _tenantId,
                CaseId = _ownerCaseId,
                FileName = "newer.png",
                ContentType = "image/png",
                SizeBytes = 20,
                StorageKey = $"tenants/{_tenantId:N}/cases/{_ownerCaseId:N}/{_docNewerId:N}/newer.png",
                UploadedByUserId = _customerId,
                UploadedAt = now
            },
            new Document
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                CaseId = _peerCaseId,
                FileName = "peer-only.pdf",
                ContentType = "application/pdf",
                SizeBytes = 5,
                StorageKey = $"tenants/{_tenantId:N}/cases/{_peerCaseId:N}/peer.pdf",
                UploadedByUserId = _peerCustomerId,
                UploadedAt = now
            },
            new Document
            {
                Id = Guid.NewGuid(),
                TenantId = _otherTenantId,
                CaseId = _otherTenantCaseId,
                FileName = "other.pdf",
                ContentType = "application/pdf",
                SizeBytes = 5,
                StorageKey = $"tenants/{_otherTenantId:N}/cases/{_otherTenantCaseId:N}/other.pdf",
                UploadedByUserId = _otherTenantCustomerId,
                UploadedAt = now
            });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Owner_lists_own_case_metadata_newest_first_without_storage_key()
    {
        Authenticate(UserRole.Customer, _customerId);
        var payload = await PostDocumentsQueryAsync(_ownerCaseId);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var docs = payload.GetProperty("data").GetProperty("documents");
        Assert.Equal(2, docs.GetArrayLength());
        Assert.Equal(_docNewerId.ToString(), docs[0].GetProperty("id").GetString());
        Assert.Equal("newer.png", docs[0].GetProperty("fileName").GetString());
        Assert.Equal("image/png", docs[0].GetProperty("contentType").GetString());
        Assert.Equal(20, docs[0].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(_customerId.ToString(), docs[0].GetProperty("uploadedBy").GetString());
        Assert.Equal(_docOlderId.ToString(), docs[1].GetProperty("id").GetString());

        var json = docs.ToString();
        Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"tenants/{_tenantId:N}", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Peer_customer_gets_NOT_FOUND_for_other_case()
    {
        Authenticate(UserRole.Customer, _peerCustomerId);
        var payload = await PostDocumentsQueryAsync(_ownerCaseId);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("NOT_FOUND", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reviewer_lists_tenant_case_documents()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);
        var payload = await PostDocumentsQueryAsync(_ownerCaseId);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal(2, payload.GetProperty("data").GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task TenantAdmin_lists_tenant_case_documents()
    {
        Authenticate(UserRole.TenantAdmin, _adminId);
        var payload = await PostDocumentsQueryAsync(_peerCaseId);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal(1, payload.GetProperty("data").GetProperty("documents").GetArrayLength());
        Assert.Equal("peer-only.pdf", payload.GetProperty("data").GetProperty("documents")[0].GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Other_tenant_case_is_NOT_FOUND()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);
        var payload = await PostDocumentsQueryAsync(_otherTenantCaseId);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("NOT_FOUND", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_case_id_is_VALIDATION()
    {
        Authenticate(UserRole.Customer, _customerId);
        var payload = await PostDocumentsQueryAsync(Guid.Empty);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("VALIDATION", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var payload = await PostDocumentsQueryAsync(_ownerCaseId);
        Assert.True(payload.TryGetProperty("errors", out _), payload.ToString());
    }

    private Task<JsonElement> PostDocumentsQueryAsync(Guid caseId) =>
        PostGraphqlAsync(
            $$"""
            {
              "query": "query($caseId: UUID!) { documents(caseId: $caseId) { id fileName contentType sizeBytes uploadedAt uploadedBy } }",
              "variables": { "caseId": "{{caseId}}" }
            }
            """);

    private async Task<JsonElement> PostGraphqlAsync(string jsonBody)
    {
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(jsonBody, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private void Authenticate(UserRole role, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = userId,
            TenantId = _tenantId,
            Email = $"{role.ToString().ToLowerInvariant()}@listdocs.example",
            PasswordHash = "unused",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
