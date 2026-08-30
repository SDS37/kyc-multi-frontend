using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class GetCaseDetailTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _reviewerId;
    private Guid _customerAId;
    private Guid _customerBId;
    private Guid _otherTenantCustomerId;
    private Guid _customerACaseId;
    private Guid _customerBCaseId;
    private Guid _otherTenantCaseId;
    private Guid _reviewedCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _otherTenantId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _customerAId = Guid.NewGuid();
        _customerBId = Guid.NewGuid();
        _otherTenantCustomerId = Guid.NewGuid();
        _customerACaseId = Guid.NewGuid();
        _customerBCaseId = Guid.NewGuid();
        _otherTenantCaseId = Guid.NewGuid();
        _reviewedCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "Detail Co",
                Slug = $"dtl-{_tenantId:N}"[..20],
                IsActive = true,
                CreatedAt = now
            },
            new Tenant
            {
                Id = _otherTenantId,
                Name = "Other Co",
                Slug = $"oth-{_otherTenantId:N}"[..20],
                IsActive = true,
                CreatedAt = now
            });
        db.Users.AddRange(
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@detail.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = _customerAId,
                TenantId = _tenantId,
                Email = "customer-a@detail.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _customerBId,
                TenantId = _tenantId,
                Email = "customer-b@detail.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
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
                Id = _customerACaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerAId,
                Title = "A case",
                Status = CaseStatus.Submitted,
                FormData = """{"fullName":"Ada","dateOfBirth":"1815-12-10","nationality":"British","address":"12 Engine Rd"}""",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now
            },
            new Case
            {
                Id = _customerBCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerBId,
                Title = "B case",
                Status = CaseStatus.Draft,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Case
            {
                Id = _reviewedCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerAId,
                Title = "Reviewed",
                Status = CaseStatus.Rejected,
                FormData = """{"fullName":"Ada"}""",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now,
                ReviewedAt = now,
                ReviewedBy = _reviewerId,
                ReviewComment = "Missing proof of address"
            },
            new Case
            {
                Id = _otherTenantCaseId,
                TenantId = _otherTenantId,
                CustomerUserId = _otherTenantCustomerId,
                Title = "Other tenant",
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
    public async Task Customer_can_open_own_case_with_formData()
    {
        Authenticate(UserRole.Customer, _customerAId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "query($id: UUID!) { case(id: $id) { case { id title status formData customerUserId } comments { text } documents { id } } }",
              "variables": { "id": "{{_customerACaseId}}" }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var detail = payload.GetProperty("data").GetProperty("case");
        var c = detail.GetProperty("case");
        Assert.Equal(_customerACaseId.ToString(), c.GetProperty("id").GetString());
        Assert.Equal("SUBMITTED", c.GetProperty("status").GetString());
        Assert.Contains("Ada", c.GetProperty("formData").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, detail.GetProperty("comments").GetArrayLength());
        Assert.Equal(0, detail.GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task Customer_cannot_open_peer_case()
    {
        Authenticate(UserRole.Customer, _customerAId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "query($id: UUID!) { case(id: $id) { case { id } } }",
                  "variables": { "id": "{{_customerBCaseId}}" }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("NOT_FOUND", errors, StringComparison.Ordinal);
        Assert.Contains(CaseVisibility.NotFoundMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reviewer_can_open_any_tenant_case()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "query($id: UUID!) { case(id: $id) { case { id title customerUserId customerEmail } documents { id fileName } } }",
              "variables": { "id": "{{_customerBCaseId}}" }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var c = payload.GetProperty("data").GetProperty("case").GetProperty("case");
        Assert.Equal(_customerBCaseId.ToString(), c.GetProperty("id").GetString());
        Assert.Equal(_customerBId.ToString(), c.GetProperty("customerUserId").GetString());
        Assert.Equal("customer-b@detail.example", c.GetProperty("customerEmail").GetString());
    }

    [Fact]
    public async Task Reviewed_case_exposes_comment_not_file_bytes()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "query($id: UUID!) { case(id: $id) { case { status reviewComment } comments { text authorUserId } documents { id } } }",
              "variables": { "id": "{{_reviewedCaseId}}" }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var detail = payload.GetProperty("data").GetProperty("case");
        Assert.Equal("REJECTED", detail.GetProperty("case").GetProperty("status").GetString());
        Assert.Equal("Missing proof of address", detail.GetProperty("case").GetProperty("reviewComment").GetString());
        Assert.Equal(1, detail.GetProperty("comments").GetArrayLength());
        Assert.Equal("Missing proof of address", detail.GetProperty("comments")[0].GetProperty("text").GetString());
        Assert.Equal(_reviewerId.ToString(), detail.GetProperty("comments")[0].GetProperty("authorUserId").GetString());
        Assert.Equal(0, detail.GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task Other_tenant_case_returns_NOT_FOUND()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "query($id: UUID!) { case(id: $id) { case { id } } }",
                  "variables": { "id": "{{_otherTenantCaseId}}" }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("NOT_FOUND", document.RootElement.GetProperty("errors").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "query($id: UUID!) { case(id: $id) { case { id } } }",
                  "variables": { "id": "{{_customerACaseId}}" }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("AUTH_NOT_AUTHENTICATED", document.RootElement.GetProperty("errors").ToString(), StringComparison.Ordinal);
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
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
