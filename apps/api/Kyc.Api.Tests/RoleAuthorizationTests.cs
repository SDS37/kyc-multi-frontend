using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class RoleAuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;
    private Guid _reviewerId;
    private Guid _submittedCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _submittedCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Role Co",
            Slug = $"role-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.AddRange(
            new User
            {
                Id = _customerId,
                TenantId = _tenantId,
                Email = "customer@role.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@role.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            });
        db.Cases.Add(new Case
        {
            Id = _submittedCaseId,
            TenantId = _tenantId,
            CustomerUserId = _customerId,
            Title = "Role gate submitted",
            Status = CaseStatus.Submitted,
            FormData = """{"fullName":"Ada"}""",
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
    public async Task Reviewer_can_call_startCaseReview()
    {
        Authenticate(UserRole.Reviewer, _tenantId, _reviewerId);
        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { status } }",
              "variables": { "input": { "id": "{{_submittedCaseId}}" } }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal("IN_REVIEW", payload.GetProperty("data").GetProperty("startCaseReview").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Customer_cannot_call_startCaseReview()
    {
        Authenticate(UserRole.Customer, _tenantId, _customerId);
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
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Contains("AUTH_NOT_AUTHORIZED", errors.ToString(), StringComparison.Ordinal);
        Assert.False(
            root.TryGetProperty("data", out var data) &&
            data.ValueKind != JsonValueKind.Null &&
            data.TryGetProperty("startCaseReview", out var value) &&
            value.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Customer_can_call_createDraftCase()
    {
        Authenticate(UserRole.Customer, _tenantId, _customerId);
        var payload = await PostGraphqlAsync(
            """
            {
              "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { title status } }",
              "variables": { "input": { "title": "Role gate draft" } }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal("Role gate draft", payload.GetProperty("data").GetProperty("createDraftCase").GetProperty("title").GetString());
        Assert.Equal("DRAFT", payload.GetProperty("data").GetProperty("createDraftCase").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reviewer_cannot_call_createDraftCase()
    {
        Authenticate(UserRole.Reviewer, _tenantId, _reviewerId);
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                """{"query":"mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id } }","variables":{"input":{"title":"Nope"}}}""",
                Encoding.UTF8,
                "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Contains("AUTH_NOT_AUTHORIZED", errors.ToString(), StringComparison.Ordinal);
    }

    private void Authenticate(UserRole role, Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
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
