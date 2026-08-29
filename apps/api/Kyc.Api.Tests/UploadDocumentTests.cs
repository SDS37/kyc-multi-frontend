using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class UploadDocumentTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _customerId;
    private Guid _peerCustomerId;
    private Guid _reviewerId;
    private Guid _otherTenantCustomerId;
    private Guid _draftCaseId;
    private Guid _submittedCaseId;
    private Guid _approvedCaseId;
    private Guid _peerCaseId;
    private Guid _otherTenantCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _otherTenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _peerCustomerId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _otherTenantCustomerId = Guid.NewGuid();
        _draftCaseId = Guid.NewGuid();
        _submittedCaseId = Guid.NewGuid();
        _approvedCaseId = Guid.NewGuid();
        _peerCaseId = Guid.NewGuid();
        _otherTenantCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "Docs Co",
                Slug = $"doc-{_tenantId:N}"[..20],
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
                Email = "customer@docs.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _peerCustomerId,
                TenantId = _tenantId,
                Email = "peer@docs.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@docs.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
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
                Id = _draftCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "Draft",
                Status = CaseStatus.Draft,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Case
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
            },
            new Case
            {
                Id = _approvedCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "Approved",
                Status = CaseStatus.Approved,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now,
                ReviewedAt = now,
                ReviewedBy = _reviewerId
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
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Owner_can_upload_pdf_to_draft_and_see_on_detail()
    {
        Authenticate(UserRole.Customer, _customerId);
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 minimal");

        using var response = await PostFileAsync(_draftCaseId, "id.pdf", "application/pdf", pdf);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("id.pdf", body.RootElement.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", body.RootElement.GetProperty("contentType").GetString());
        Assert.Equal(_customerId.ToString(), body.RootElement.GetProperty("uploadedBy").GetString());

        var detail = await PostGraphqlAsync(
            $$"""
            {
              "query": "query($id: UUID!) { case(id: $id) { documents { id fileName contentType sizeBytes uploadedBy } } }",
              "variables": { "id": "{{_draftCaseId}}" }
            }
            """);
        Assert.False(detail.TryGetProperty("errors", out _), detail.ToString());
        var docs = detail.GetProperty("data").GetProperty("case").GetProperty("documents");
        Assert.Equal(1, docs.GetArrayLength());
        Assert.Equal(id.ToString(), docs[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Owner_can_upload_png_to_submitted()
    {
        Authenticate(UserRole.Customer, _customerId);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };

        using var response = await PostFileAsync(_submittedCaseId, "shot.png", "image/png", png);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Peer_owner_gets_NOT_FOUND()
    {
        Authenticate(UserRole.Customer, _customerId);
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 x");

        using var response = await PostFileAsync(_peerCaseId, "x.pdf", "application/pdf", pdf);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("NOT_FOUND", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reviewer_gets_forbidden()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 x");

        using var response = await PostFileAsync(_draftCaseId, "x.pdf", "application/pdf", pdf);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approved_case_returns_DOMAIN()
    {
        Authenticate(UserRole.Customer, _customerId);
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 x");

        using var response = await PostFileAsync(_approvedCaseId, "x.pdf", "application/pdf", pdf);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("DOMAIN", text, StringComparison.Ordinal);
        Assert.Contains(UploadDocumentService.NotUploadableMessage, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_magic_bytes_returns_VALIDATION()
    {
        Authenticate(UserRole.Customer, _customerId);
        var fake = Encoding.ASCII.GetBytes("not-a-pdf");

        using var response = await PostFileAsync(_draftCaseId, "x.pdf", "application/pdf", fake);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Oversized_file_returns_VALIDATION()
    {
        Authenticate(UserRole.Customer, _customerId);
        var huge = new byte[DocumentUploadValidation.MaxFileBytes + 1];
        huge[0] = (byte)'%';
        huge[1] = (byte)'P';
        huge[2] = (byte)'D';
        huge[3] = (byte)'F';

        using var response = await PostFileAsync(_draftCaseId, "big.pdf", "application/pdf", huge);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Sanitize_and_magic_helpers()
    {
        Assert.Equal("a.pdf", DocumentUploadValidation.SanitizeFileName("../a.pdf"));
        Assert.Null(DocumentUploadValidation.NormalizeContentType("application/zip"));
        Assert.Equal("image/jpeg", DocumentUploadValidation.NormalizeContentType("image/jpg"));
        Assert.True(DocumentUploadValidation.MatchesMagicBytes("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
    }

    private async Task<HttpResponseMessage> PostFileAsync(Guid caseId, string fileName, string contentType, byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return await _client.PostAsync($"/api/cases/{caseId}/documents", content);
    }

    private void Authenticate(UserRole role, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = userId,
            TenantId = _tenantId,
            Email = $"{role.ToString().ToLowerInvariant()}@example.com",
            PasswordHash = "unused",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

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
}
