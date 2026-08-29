using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class StartCaseReviewTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _reviewerId;
    private Guid _adminId;
    private Guid _customerId;
    private Guid _otherTenantCustomerId;
    private Guid _submittedCaseId;
    private Guid _draftCaseId;
    private Guid _otherTenantSubmittedId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _otherTenantId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _adminId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _otherTenantCustomerId = Guid.NewGuid();
        _submittedCaseId = Guid.NewGuid();
        _draftCaseId = Guid.NewGuid();
        _otherTenantSubmittedId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "Review Co",
                Slug = $"rev-{_tenantId:N}"[..20],
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
                Email = "reviewer@rev.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = _adminId,
                TenantId = _tenantId,
                Email = "admin@rev.example",
                PasswordHash = "unused",
                Role = UserRole.TenantAdmin,
                CreatedAt = now
            },
            new User
            {
                Id = _customerId,
                TenantId = _tenantId,
                Email = "customer@rev.example",
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
                Id = _submittedCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "Submitted case",
                Status = CaseStatus.Submitted,
                FormData = """{"fullName":"Ada"}""",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now
            },
            new Case
            {
                Id = _draftCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "Still draft",
                Status = CaseStatus.Draft,
                FormData = "{}",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Case
            {
                Id = _otherTenantSubmittedId,
                TenantId = _otherTenantId,
                CustomerUserId = _otherTenantCustomerId,
                Title = "Other tenant",
                Status = CaseStatus.Submitted,
                FormData = """{"fullName":"Bob"}""",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now
            });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Reviewer_can_start_review_on_submitted_case()
    {
        // Fresh submitted case so tests stay order-independent.
        var caseId = await SeedSubmittedCaseAsync();
        Authenticate(UserRole.Reviewer, _reviewerId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}" } }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal("IN_REVIEW", payload.GetProperty("data").GetProperty("startCaseReview").GetProperty("status").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == caseId);
        Assert.Equal(CaseStatus.InReview, row.Status);
        Assert.Equal(_reviewerId, row.ReviewedBy);
        Assert.Null(row.ReviewedAt);
    }

    [Fact]
    public async Task TenantAdmin_can_start_review()
    {
        var caseId = await SeedSubmittedCaseAsync();
        Authenticate(UserRole.TenantAdmin, _adminId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}" } }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal("IN_REVIEW", payload.GetProperty("data").GetProperty("startCaseReview").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Customer_cannot_start_review()
    {
        Authenticate(UserRole.Customer, _customerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_submittedCaseId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("AUTH_NOT_AUTHORIZED", document.RootElement.GetProperty("errors").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_submitted_returns_DOMAIN()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_draftCaseId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.Contains(StartCaseReviewService.NotSubmittedMessage, errors, StringComparison.Ordinal);
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
                  "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_otherTenantSubmittedId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("NOT_FOUND", errors, StringComparison.Ordinal);
        Assert.Contains(StartCaseReviewService.NotFoundMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_reviewer_JWT_returns_AUTH_FAILED()
    {
        Authenticate(UserRole.Reviewer, Guid.NewGuid());

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_submittedCaseId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("AUTH_FAILED", errors, StringComparison.Ordinal);
        Assert.Contains(CreateDraftCaseService.GenericAuthFailure, errors, StringComparison.Ordinal);
    }

    private async Task<Guid> SeedSubmittedCaseAsync()
    {
        var caseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Cases.Add(new Case
        {
            Id = caseId,
            TenantId = _tenantId,
            CustomerUserId = _customerId,
            Title = "Fresh submitted",
            Status = CaseStatus.Submitted,
            FormData = """{"fullName":"Ada"}""",
            CreatedAt = now,
            UpdatedAt = now,
            SubmittedAt = now
        });
        await db.SaveChangesAsync();
        return caseId;
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
