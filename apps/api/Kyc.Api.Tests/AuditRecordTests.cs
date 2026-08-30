using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

/// <summary>KYC-050 — append-only audit rows for key case/document actions.</summary>
public sealed class AuditRecordTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string CompleteFormData = """
        {
          "fullName": "Ada Lovelace",
          "dateOfBirth": "1815-12-10",
          "nationality": "British",
          "address": "12 Analytical Engine Rd"
        }
        """;

    private static readonly byte[] PdfBytes = "%PDF-1.4 audit"u8.ToArray();

    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _customerId;
    private Guid _reviewerId;
    private Guid _otherTenantCustomerId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _otherTenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _otherTenantCustomerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "Audit Co",
                Slug = $"aud-{_tenantId:N}"[..20],
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
                Email = "customer@audit.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@audit.example",
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
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Approve_path_records_lifecycle_actions()
    {
        Authenticate(UserRole.Customer, _customerId);

        var caseId = await CreateDraftAsync("Audit draft", "{}");
        await AssertAuditAsync(AuditActions.CaseCreated, AuditEntityTypes.Case, caseId, _customerId);

        await UpdateDraftAsync(caseId, "Audit draft updated", CompleteFormData);
        await AssertAuditAsync(AuditActions.CaseUpdated, AuditEntityTypes.Case, caseId, _customerId);

        await SubmitAsync(caseId);
        await AssertAuditAsync(AuditActions.CaseSubmitted, AuditEntityTypes.Case, caseId, _customerId);

        Authenticate(UserRole.Reviewer, _reviewerId);
        await StartReviewAsync(caseId);
        await AssertAuditAsync(AuditActions.ReviewStarted, AuditEntityTypes.Case, caseId, _reviewerId);

        await ApproveAsync(caseId);
        await AssertAuditAsync(AuditActions.CaseApproved, AuditEntityTypes.Case, caseId, _reviewerId);
    }

    [Fact]
    public async Task Reject_records_CaseRejected()
    {
        Authenticate(UserRole.Customer, _customerId);
        var caseId = await CreateDraftAsync("Reject me", CompleteFormData);
        await SubmitAsync(caseId);

        Authenticate(UserRole.Reviewer, _reviewerId);
        await StartReviewAsync(caseId);
        await RejectAsync(caseId, "Missing proof");
        await AssertAuditAsync(AuditActions.CaseRejected, AuditEntityTypes.Case, caseId, _reviewerId);
    }

    [Fact]
    public async Task Document_upload_records_DocumentUploaded_without_storage_key()
    {
        Authenticate(UserRole.Customer, _customerId);
        var caseId = await CreateDraftAsync("Docs", "{}");

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(PdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "id.pdf");

        using var response = await _client.PostAsync($"/api/cases/{caseId}/documents", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var docJson = JsonDocument.Parse(body);
        var documentId = Guid.Parse(docJson.RootElement.GetProperty("id").GetString()!);

        var entry = await AssertAuditAsync(
            AuditActions.DocumentUploaded,
            AuditEntityTypes.Document,
            documentId,
            _customerId);

        Assert.False(string.IsNullOrWhiteSpace(entry.Payload));
        Assert.Contains(caseId.ToString(), entry.Payload, StringComparison.Ordinal);
        Assert.Contains("id.pdf", entry.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("storageKey", entry.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenants/", entry.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Audit_rows_are_tenant_filtered()
    {
        Authenticate(UserRole.Customer, _customerId);
        var caseId = await CreateDraftAsync("Tenant A", "{}");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Simulate other-tenant JWT context by querying with IgnoreQueryFilters after seeding a row for B.
            db.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.NewGuid(),
                TenantId = _otherTenantId,
                ActorUserId = _otherTenantCustomerId,
                EntityType = AuditEntityTypes.Case,
                EntityId = Guid.NewGuid(),
                Action = AuditActions.CaseCreated,
                OccurredAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            // Jwt in this host is not set on scoped HttpCurrentTenant — tests use factory without HTTP for EF.
            // Assert via IgnoreQueryFilters counts + filtered count under null tenant (fail closed = 0).
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var all = await db.AuditEntries.IgnoreQueryFilters().CountAsync();
            Assert.True(all >= 2);

            var visibleWithoutTenant = await db.AuditEntries.CountAsync();
            Assert.Equal(0, visibleWithoutTenant);
        }

        // Owner case still has its CaseCreated when filters ignored for assertion helper already used.
        await AssertAuditAsync(AuditActions.CaseCreated, AuditEntityTypes.Case, caseId, _customerId);
    }

    [Fact]
    public async Task Schema_has_no_audit_mutations()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                """{"query":"{ __type(name: \"Mutation\") { fields { name } } }"}""",
                Encoding.UTF8,
                "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.DoesNotContain("audit", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid> CreateDraftAsync(string title, string formData)
    {
        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id } }",
              "variables": { "input": { "title": {{JsonSerializer.Serialize(title)}}, "formData": {{JsonSerializer.Serialize(formData)}} } }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        return Guid.Parse(payload.GetProperty("data").GetProperty("createDraftCase").GetProperty("id").GetString()!);
    }

    private async Task UpdateDraftAsync(Guid caseId, string title, string formData)
    {
        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
              "variables": {
                "input": {
                  "id": "{{caseId}}",
                  "title": {{JsonSerializer.Serialize(title)}},
                  "formData": {{JsonSerializer.Serialize(formData)}}
                }
              }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
    }

    private async Task SubmitAsync(Guid caseId)
    {
        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}" } }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
    }

    private async Task StartReviewAsync(Guid caseId)
    {
        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}" } }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
    }

    private async Task ApproveAsync(Guid caseId)
    {
        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: ApproveCaseRequestInput!) { approveCase(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}", "comment": null } }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
    }

    private async Task RejectAsync(Guid caseId, string comment)
    {
        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: RejectCaseRequestInput!) { rejectCase(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}", "comment": {{JsonSerializer.Serialize(comment)}} } }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
    }

    private async Task<AuditEntry> AssertAuditAsync(
        string action,
        string entityType,
        Guid entityId,
        Guid actorUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditEntries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(e =>
                e.Action == action &&
                e.EntityType == entityType &&
                e.EntityId == entityId);

        Assert.NotNull(entry);
        Assert.Equal(_tenantId, entry.TenantId);
        Assert.Equal(actorUserId, entry.ActorUserId);
        Assert.True(entry.OccurredAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        return entry;
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

    private void Authenticate(UserRole role, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = userId,
            TenantId = _tenantId,
            Email = $"{role.ToString().ToLowerInvariant()}@audit.example",
            PasswordHash = "unused",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
