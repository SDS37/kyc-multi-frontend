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

public sealed class UpdateDraftCaseTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _ownerId;
    private Guid _otherCustomerId;
    private Guid _draftCaseId;
    private Guid _submittedCaseId;

    public UpdateDraftCaseTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _ownerId = Guid.NewGuid();
        _otherCustomerId = Guid.NewGuid();
        _draftCaseId = Guid.NewGuid();
        _submittedCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Update Co",
            Slug = $"upd-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.AddRange(
            new User
            {
                Id = _ownerId,
                TenantId = _tenantId,
                Email = "owner@upd.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _otherCustomerId,
                TenantId = _tenantId,
                Email = "other@upd.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            });
        db.Cases.AddRange(
            new Case
            {
                Id = _draftCaseId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Original title",
                Status = CaseStatus.Draft,
                FormData = """{"step":1}""",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Case
            {
                Id = _submittedCaseId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Submitted case",
                Status = CaseStatus.Submitted,
                FormData = """{"step":2}""",
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
    public async Task Owner_can_update_draft_title_and_formData()
    {
        Authenticate(_ownerId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id title status formData } }",
              "variables": {
                "input": {
                  "id": "{{_draftCaseId}}",
                  "title": "Updated title",
                  "formData": "{\"step\":3}"
                }
              }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var updated = payload.GetProperty("data").GetProperty("updateDraftCase");
        Assert.Equal("Updated title", updated.GetProperty("title").GetString());
        Assert.Equal("DRAFT", updated.GetProperty("status").GetString());
        Assert.Equal("""{"step":3}""", updated.GetProperty("formData").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == _draftCaseId);
        Assert.Equal("Updated title", row.Title);
        Assert.Equal("""{"step":3}""", row.FormData);
        Assert.Equal(CaseStatus.Draft, row.Status);
    }

    [Fact]
    public async Task Non_owner_cannot_update_draft()
    {
        Authenticate(_otherCustomerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
                  "variables": {
                    "input": { "id": "{{_draftCaseId}}", "title": "Hijack" }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.Contains(UpdateDraftCaseService.NotOwnerMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_draft_status_returns_domain_error()
    {
        Authenticate(_ownerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
                  "variables": {
                    "input": { "id": "{{_submittedCaseId}}", "title": "Too late" }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.Contains(UpdateDraftCaseService.NotDraftMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reviewer_cannot_update_draft()
    {
        AuthenticateRole(UserRole.Reviewer, Guid.NewGuid());

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
                  "variables": {
                    "input": { "id": "{{_draftCaseId}}", "title": "Nope" }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("AUTH_NOT_AUTHORIZED", document.RootElement.GetProperty("errors").ToString(), StringComparison.Ordinal);
    }

    private void Authenticate(Guid userId) => AuthenticateRole(UserRole.Customer, userId);

    private void AuthenticateRole(UserRole role, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
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
