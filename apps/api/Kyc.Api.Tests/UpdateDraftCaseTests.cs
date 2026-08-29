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

public sealed class UpdateDraftCaseTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _ownerId;
    private Guid _otherCustomerId;
    private Guid _draftCaseId;
    private Guid _omitFormDataCaseId;
    private Guid _submittedCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _ownerId = Guid.NewGuid();
        _otherCustomerId = Guid.NewGuid();
        _draftCaseId = Guid.NewGuid();
        _omitFormDataCaseId = Guid.NewGuid();
        _submittedCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
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
                Id = _omitFormDataCaseId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Omit form title",
                Status = CaseStatus.Draft,
                FormData = """{"keep":true}""",
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

        using var scope = factory.Services.CreateScope();
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
        Assert.Contains("NOT_FOUND", errors, StringComparison.Ordinal);
        Assert.Contains(UpdateDraftCaseService.NotFoundMessage, errors, StringComparison.Ordinal);
        Assert.DoesNotContain("DOMAIN", errors, StringComparison.Ordinal);
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
    public async Task Non_draft_with_invalid_formData_still_returns_DOMAIN()
    {
        Authenticate(_ownerId);
        var oversized = $$"""{"pad":"{{new string('x', CreateDraftCaseService.MaxFormDataUtf8Bytes)}}"}""";

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
                  "variables": {
                    "input": {
                      "id": "{{_submittedCaseId}}",
                      "title": "",
                      "formData": {{JsonSerializer.Serialize(oversized)}}
                    }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", errors, StringComparison.Ordinal);
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

    [Fact]
    public async Task Omitting_formData_leaves_existing_formData_unchanged()
    {
        Authenticate(_ownerId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id title formData } }",
              "variables": {
                "input": {
                  "id": "{{_omitFormDataCaseId}}",
                  "title": "Title only"
                }
              }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var updated = payload.GetProperty("data").GetProperty("updateDraftCase");
        Assert.Equal("Title only", updated.GetProperty("title").GetString());
        Assert.Equal("""{"keep":true}""", updated.GetProperty("formData").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == _omitFormDataCaseId);
        Assert.Equal("""{"keep":true}""", row.FormData);
    }

    [Fact]
    public async Task Missing_case_returns_NOT_FOUND()
    {
        Authenticate(_ownerId);
        var missingId = Guid.NewGuid();

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
                  "variables": {
                    "input": { "id": "{{missingId}}", "title": "Ghost" }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("NOT_FOUND", errors, StringComparison.Ordinal);
        Assert.Contains(UpdateDraftCaseService.NotFoundMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Oversized_formData_returns_VALIDATION()
    {
        Authenticate(_ownerId);
        var oversized = $$"""{"pad":"{{new string('x', CreateDraftCaseService.MaxFormDataUtf8Bytes)}}"}""";

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
                  "variables": {
                    "input": {
                      "id": "{{_draftCaseId}}",
                      "title": "Too big",
                      "formData": {{JsonSerializer.Serialize(oversized)}}
                    }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains("65536", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deeply_nested_formData_returns_VALIDATION()
    {
        Authenticate(_ownerId);
        var nested = "{}";
        for (var i = 0; i < CreateDraftCaseService.MaxFormDataDepth + 2; i++)
        {
            nested = $$"""{"a":{{nested}}}""";
        }

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
                  "variables": {
                    "input": {
                      "id": "{{_draftCaseId}}",
                      "title": "Too deep",
                      "formData": {{JsonSerializer.Serialize(nested)}}
                    }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains("valid JSON", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_customer_JWT_returns_AUTH_FAILED()
    {
        Authenticate(Guid.NewGuid());

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id } }",
                  "variables": {
                    "input": { "id": "{{_draftCaseId}}", "title": "Stale" }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("AUTH_FAILED", errors, StringComparison.Ordinal);
        Assert.Contains(CreateDraftCaseService.GenericAuthFailure, errors, StringComparison.Ordinal);
    }

    private void Authenticate(Guid userId) => AuthenticateRole(UserRole.Customer, userId);

    private void AuthenticateRole(UserRole role, Guid userId)
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
