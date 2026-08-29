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

public sealed class CompleteCaseReviewTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _reviewerId;
    private Guid _adminId;
    private Guid _customerId;
    private Guid _otherTenantCustomerId;
    private Guid _inReviewCaseId;
    private Guid _submittedCaseId;
    private Guid _otherTenantInReviewId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _otherTenantId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _adminId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _otherTenantCustomerId = Guid.NewGuid();
        _inReviewCaseId = Guid.NewGuid();
        _submittedCaseId = Guid.NewGuid();
        _otherTenantInReviewId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "Decide Co",
                Slug = $"dec-{_tenantId:N}"[..20],
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
                Email = "reviewer@dec.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = _adminId,
                TenantId = _tenantId,
                Email = "admin@dec.example",
                PasswordHash = "unused",
                Role = UserRole.TenantAdmin,
                CreatedAt = now
            },
            new User
            {
                Id = _customerId,
                TenantId = _tenantId,
                Email = "customer@dec.example",
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
                Id = _inReviewCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "In review",
                Status = CaseStatus.InReview,
                FormData = """{"fullName":"Ada"}""",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now,
                ReviewedBy = _reviewerId
            },
            new Case
            {
                Id = _submittedCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "Still submitted",
                Status = CaseStatus.Submitted,
                FormData = """{"fullName":"Ada"}""",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now
            },
            new Case
            {
                Id = _otherTenantInReviewId,
                TenantId = _otherTenantId,
                CustomerUserId = _otherTenantCustomerId,
                Title = "Other tenant",
                Status = CaseStatus.InReview,
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
    public async Task Reviewer_can_approve_in_review_case_without_comment()
    {
        var caseId = await SeedInReviewCaseAsync();
        Authenticate(UserRole.Reviewer, _reviewerId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: ApproveCaseRequestInput!) { approveCase(input: $input) { id status reviewedAt reviewedBy reviewComment } }",
              "variables": { "input": { "id": "{{caseId}}" } }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var approved = payload.GetProperty("data").GetProperty("approveCase");
        Assert.Equal("APPROVED", approved.GetProperty("status").GetString());
        Assert.False(approved.GetProperty("reviewedAt").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
        Assert.Equal(_reviewerId.ToString(), approved.GetProperty("reviewedBy").GetString());
        Assert.Equal(JsonValueKind.Null, approved.GetProperty("reviewComment").ValueKind);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == caseId);
        Assert.Equal(CaseStatus.Approved, row.Status);
        Assert.Equal(_reviewerId, row.ReviewedBy);
        Assert.NotNull(row.ReviewedAt);
        Assert.Null(row.ReviewComment);
    }

    [Fact]
    public async Task TenantAdmin_can_approve_with_optional_comment()
    {
        var caseId = await SeedInReviewCaseAsync();
        Authenticate(UserRole.TenantAdmin, _adminId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: ApproveCaseRequestInput!) { approveCase(input: $input) { status reviewComment reviewedBy } }",
              "variables": { "input": { "id": "{{caseId}}", "comment": "Looks good" } }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var approved = payload.GetProperty("data").GetProperty("approveCase");
        Assert.Equal("APPROVED", approved.GetProperty("status").GetString());
        Assert.Equal("Looks good", approved.GetProperty("reviewComment").GetString());
        Assert.Equal(_adminId.ToString(), approved.GetProperty("reviewedBy").GetString());
    }

    [Fact]
    public async Task Reviewer_can_reject_with_comment()
    {
        var caseId = await SeedInReviewCaseAsync();
        Authenticate(UserRole.Reviewer, _reviewerId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: RejectCaseRequestInput!) { rejectCase(input: $input) { status reviewComment reviewedAt reviewedBy } }",
              "variables": { "input": { "id": "{{caseId}}", "comment": "Missing proof of address" } }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var rejected = payload.GetProperty("data").GetProperty("rejectCase");
        Assert.Equal("REJECTED", rejected.GetProperty("status").GetString());
        Assert.Equal("Missing proof of address", rejected.GetProperty("reviewComment").GetString());
        Assert.Equal(_reviewerId.ToString(), rejected.GetProperty("reviewedBy").GetString());
        Assert.False(rejected.GetProperty("reviewedAt").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
    }

    [Fact]
    public async Task Reject_without_comment_returns_VALIDATION()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: RejectCaseRequestInput!) { rejectCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_inReviewCaseId}}", "comment": "   " } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains(CompleteCaseReviewService.RejectCommentRequiredMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Customer_cannot_approve_or_reject()
    {
        Authenticate(UserRole.Customer, _customerId);

        using var approveResponse = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: ApproveCaseRequestInput!) { approveCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_inReviewCaseId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, approveResponse.StatusCode);
        using var approveDoc = JsonDocument.Parse(await approveResponse.Content.ReadAsStringAsync());
        Assert.Contains("AUTH_NOT_AUTHORIZED", approveDoc.RootElement.GetProperty("errors").ToString(), StringComparison.Ordinal);

        using var rejectResponse = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: RejectCaseRequestInput!) { rejectCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_inReviewCaseId}}", "comment": "nope" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, rejectResponse.StatusCode);
        using var rejectDoc = JsonDocument.Parse(await rejectResponse.Content.ReadAsStringAsync());
        Assert.Contains("AUTH_NOT_AUTHORIZED", rejectDoc.RootElement.GetProperty("errors").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_in_review_returns_DOMAIN()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: ApproveCaseRequestInput!) { approveCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_submittedCaseId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.Contains(CompleteCaseReviewService.NotInReviewMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_in_review_reject_without_comment_returns_DOMAIN()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: RejectCaseRequestInput!) { rejectCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_submittedCaseId}}", "comment": "   " } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains(CompleteCaseReviewService.NotInReviewMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_case_reject_without_comment_returns_NOT_FOUND()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);
        var missingId = Guid.NewGuid();

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: RejectCaseRequestInput!) { rejectCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{missingId}}", "comment": "   " } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("NOT_FOUND", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains(CompleteCaseReviewService.NotFoundMessage, errors, StringComparison.Ordinal);
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
                  "query": "mutation($input: ApproveCaseRequestInput!) { approveCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_otherTenantInReviewId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("NOT_FOUND", errors, StringComparison.Ordinal);
        Assert.Contains(CompleteCaseReviewService.NotFoundMessage, errors, StringComparison.Ordinal);
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
                  "query": "mutation($input: ApproveCaseRequestInput!) { approveCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_inReviewCaseId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("AUTH_FAILED", errors, StringComparison.Ordinal);
        Assert.Contains(CreateDraftCaseService.GenericAuthFailure, errors, StringComparison.Ordinal);
    }

    private async Task<Guid> SeedInReviewCaseAsync()
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
            Title = "Fresh in review",
            Status = CaseStatus.InReview,
            FormData = """{"fullName":"Ada"}""",
            CreatedAt = now,
            UpdatedAt = now,
            SubmittedAt = now,
            ReviewedBy = _reviewerId
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
