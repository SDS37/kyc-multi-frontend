using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Documents;
using Kyc.Api.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

/// <summary>KYC-051 — Reviewer/TenantAdmin case audit history; customers blocked; newest first.</summary>
public sealed class ListCaseAuditTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _customerId;
    private Guid _reviewerId;
    private Guid _adminId;
    private Guid _otherTenantReviewerId;
    private Guid _caseId;
    private Guid _otherTenantCaseId;
    private Guid _documentId;
    private Guid _auditOlderId;
    private Guid _auditNewerId;
    private Guid _auditDocId;
    private Guid _auditOtherCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _otherTenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _adminId = Guid.NewGuid();
        _otherTenantReviewerId = Guid.NewGuid();
        var otherTenantCustomerId = Guid.NewGuid();
        _caseId = Guid.NewGuid();
        _otherTenantCaseId = Guid.NewGuid();
        _documentId = Guid.NewGuid();
        _auditOlderId = Guid.NewGuid();
        _auditNewerId = Guid.NewGuid();
        _auditDocId = Guid.NewGuid();
        _auditOtherCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "Audit View Co",
                Slug = $"avc-{_tenantId:N}"[..20],
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
                Email = "customer@auditview.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@auditview.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = _adminId,
                TenantId = _tenantId,
                Email = "admin@auditview.example",
                PasswordHash = "unused",
                Role = UserRole.TenantAdmin,
                CreatedAt = now
            },
            new User
            {
                Id = _otherTenantReviewerId,
                TenantId = _otherTenantId,
                Email = "reviewer@oth.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = otherTenantCustomerId,
                TenantId = _otherTenantId,
                Email = "customer@oth.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            });
        db.Cases.AddRange(
            new Case
            {
                Id = _caseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "Audited",
                Status = CaseStatus.Submitted,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now
            },
            new Case
            {
                Id = _otherTenantCaseId,
                TenantId = _otherTenantId,
                CustomerUserId = otherTenantCustomerId,
                Title = "Other",
                Status = CaseStatus.Draft,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now
            });
        db.Documents.Add(new Document
        {
            Id = _documentId,
            TenantId = _tenantId,
            CaseId = _caseId,
            FileName = "id.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            StorageKey = $"tenants/{_tenantId:N}/cases/{_caseId:N}/{_documentId:N}/id.pdf",
            UploadedByUserId = _customerId,
            UploadedAt = now
        });
        db.AuditEntries.AddRange(
            new AuditEntry
            {
                Id = _auditOlderId,
                TenantId = _tenantId,
                ActorUserId = _customerId,
                EntityType = AuditEntityTypes.Case,
                EntityId = _caseId,
                Action = AuditActions.CaseCreated,
                OccurredAt = now.AddMinutes(-10),
                Payload = null
            },
            new AuditEntry
            {
                Id = _auditNewerId,
                TenantId = _tenantId,
                ActorUserId = _customerId,
                EntityType = AuditEntityTypes.Case,
                EntityId = _caseId,
                Action = AuditActions.CaseSubmitted,
                OccurredAt = now.AddMinutes(-1),
                Payload = null
            },
            new AuditEntry
            {
                Id = _auditDocId,
                TenantId = _tenantId,
                ActorUserId = _customerId,
                EntityType = AuditEntityTypes.Document,
                EntityId = _documentId,
                Action = AuditActions.DocumentUploaded,
                OccurredAt = now.AddMinutes(-5),
                Payload = $$"""{"caseId":"{{_caseId}}","fileName":"id.pdf"}"""
            },
            new AuditEntry
            {
                Id = _auditOtherCaseId,
                TenantId = _tenantId,
                ActorUserId = _customerId,
                EntityType = AuditEntityTypes.Case,
                EntityId = Guid.NewGuid(),
                Action = AuditActions.CaseCreated,
                OccurredAt = now,
                Payload = null
            },
            new AuditEntry
            {
                Id = Guid.NewGuid(),
                TenantId = _otherTenantId,
                ActorUserId = _otherTenantReviewerId,
                EntityType = AuditEntityTypes.Case,
                EntityId = _otherTenantCaseId,
                Action = AuditActions.CaseCreated,
                OccurredAt = now,
                Payload = null
            });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Reviewer_lists_case_and_document_audits_newest_first()
    {
        Authenticate(UserRole.Reviewer, _reviewerId, _tenantId);
        var payload = await PostCaseAuditQueryAsync(_caseId);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var items = payload.GetProperty("data").GetProperty("caseAuditEntries");
        Assert.Equal(3, items.GetArrayLength());
        Assert.Equal(_auditNewerId.ToString(), items[0].GetProperty("id").GetString());
        Assert.Equal(AuditActions.CaseSubmitted, items[0].GetProperty("action").GetString());
        Assert.Equal(_auditDocId.ToString(), items[1].GetProperty("id").GetString());
        Assert.Equal(AuditActions.DocumentUploaded, items[1].GetProperty("action").GetString());
        Assert.Equal(_auditOlderId.ToString(), items[2].GetProperty("id").GetString());
        Assert.Equal(AuditActions.CaseCreated, items[2].GetProperty("action").GetString());

        Assert.Equal(_customerId.ToString(), items[0].GetProperty("actorUserId").GetString());
        Assert.DoesNotContain("storageKey", items.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"tenants/{_tenantId:N}", items.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TenantAdmin_can_list_case_audit()
    {
        Authenticate(UserRole.TenantAdmin, _adminId, _tenantId);
        var payload = await PostCaseAuditQueryAsync(_caseId);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal(3, payload.GetProperty("data").GetProperty("caseAuditEntries").GetArrayLength());
    }

    [Fact]
    public async Task Customer_cannot_read_case_audit()
    {
        Authenticate(UserRole.Customer, _customerId, _tenantId);
        var payload = await PostCaseAuditQueryAsync(_caseId);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("AUTH_NOT_AUTHORIZED", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Other_tenant_case_is_NOT_FOUND()
    {
        Authenticate(UserRole.Reviewer, _reviewerId, _tenantId);
        var payload = await PostCaseAuditQueryAsync(_otherTenantCaseId);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("NOT_FOUND", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_case_id_is_VALIDATION()
    {
        Authenticate(UserRole.Reviewer, _reviewerId, _tenantId);
        var payload = await PostCaseAuditQueryAsync(Guid.Empty);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("VALIDATION", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var payload = await PostCaseAuditQueryAsync(_caseId);
        Assert.True(payload.TryGetProperty("errors", out _), payload.ToString());
    }

    private Task<JsonElement> PostCaseAuditQueryAsync(Guid caseId) =>
        PostGraphqlAsync(
            $$"""
            {
              "query": "query($caseId: UUID!) { caseAuditEntries(caseId: $caseId) { id entityType entityId action actorUserId occurredAt payload } }",
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

    private void Authenticate(UserRole role, Guid userId, Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = $"{role.ToString().ToLowerInvariant()}@auditview.example",
            PasswordHash = "unused",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
