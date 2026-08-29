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

public sealed class SubmitCaseTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string CompleteFormData = """
        {
          "fullName": "Ada Lovelace",
          "dateOfBirth": "1815-12-10",
          "nationality": "British",
          "address": "12 Analytical Engine Rd",
          "companyName": "Optional Co"
        }
        """;

    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _ownerId;
    private Guid _otherCustomerId;
    private Guid _readyDraftId;
    private Guid _incompleteDraftId;
    private Guid _submittedCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _ownerId = Guid.NewGuid();
        _otherCustomerId = Guid.NewGuid();
        _readyDraftId = Guid.NewGuid();
        _incompleteDraftId = Guid.NewGuid();
        _submittedCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Submit Co",
            Slug = $"sub-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.AddRange(
            new User
            {
                Id = _ownerId,
                TenantId = _tenantId,
                Email = "owner@sub.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _otherCustomerId,
                TenantId = _tenantId,
                Email = "other@sub.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            });
        db.Cases.AddRange(
            new Case
            {
                Id = _readyDraftId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Ready to submit",
                Status = CaseStatus.Draft,
                FormData = CompleteFormData,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Case
            {
                Id = _incompleteDraftId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Incomplete",
                Status = CaseStatus.Draft,
                FormData = """{"fullName":"Only name"}""",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Case
            {
                Id = _submittedCaseId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Already submitted",
                Status = CaseStatus.Submitted,
                FormData = CompleteFormData,
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
    public async Task Owner_can_submit_complete_draft()
    {
        // Fresh draft per test — do not mutate shared class seed (order-independent).
        var draftId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Cases.Add(new Case
            {
                Id = draftId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Fresh submit",
                Status = CaseStatus.Draft,
                FormData = CompleteFormData,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        Authenticate(_ownerId);

        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id status submittedAt } }",
              "variables": { "input": { "id": "{{draftId}}" } }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var submitted = payload.GetProperty("data").GetProperty("submitCase");
        Assert.Equal("SUBMITTED", submitted.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, submitted.GetProperty("submittedAt").ValueKind);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == draftId);
            Assert.Equal(CaseStatus.Submitted, row.Status);
            Assert.NotNull(row.SubmittedAt);
        }
    }

    [Fact]
    public async Task Incomplete_formData_returns_VALIDATION()
    {
        Authenticate(_ownerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_incompleteDraftId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains("dateOfBirth", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_owner_cannot_submit()
    {
        Authenticate(_otherCustomerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_readyDraftId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("NOT_FOUND", errors, StringComparison.Ordinal);
        Assert.Contains(SubmitCaseService.NotFoundMessage, errors, StringComparison.Ordinal);
        Assert.DoesNotContain("DOMAIN", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_draft_returns_DOMAIN()
    {
        Authenticate(_ownerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_submittedCaseId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.Contains(SubmitCaseService.NotDraftMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_draft_with_invalid_formData_still_returns_DOMAIN()
    {
        var submittedId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Cases.Add(new Case
            {
                Id = submittedId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Submitted incomplete",
                Status = CaseStatus.Submitted,
                FormData = """{"fullName":"Only name"}""",
                CreatedAt = now,
                UpdatedAt = now,
                SubmittedAt = now
            });
            await db.SaveChangesAsync();
        }

        Authenticate(_ownerId);
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{submittedId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains(SubmitCaseService.NotDraftMessage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Oversized_formData_returns_VALIDATION()
    {
        var draftId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var oversized = $$"""{"pad":"{{new string('x', CreateDraftCaseService.MaxFormDataUtf8Bytes)}}"}""";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Cases.Add(new Case
            {
                Id = draftId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Huge form",
                Status = CaseStatus.Draft,
                FormData = oversized,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        Authenticate(_ownerId);
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{draftId}}" } }
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
    public async Task Whitespace_padded_complete_formData_still_submits()
    {
        var draftId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Cases.Add(new Case
            {
                Id = draftId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Padded form",
                Status = CaseStatus.Draft,
                FormData = $"  \n{CompleteFormData}\n  ",
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        Authenticate(_ownerId);
        var payload = await PostGraphqlAsync(
            $$"""
            {
              "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id status } }",
              "variables": { "input": { "id": "{{draftId}}" } }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal("SUBMITTED", payload.GetProperty("data").GetProperty("submitCase").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Deeply_nested_formData_returns_VALIDATION()
    {
        var draftId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var nested = "{}";
        for (var i = 0; i < CreateDraftCaseService.MaxFormDataDepth + 2; i++)
        {
            nested = $$"""{"a":{{nested}}}""";
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Cases.Add(new Case
            {
                Id = draftId,
                TenantId = _tenantId,
                CustomerUserId = _ownerId,
                Title = "Deep form",
                Status = CaseStatus.Draft,
                FormData = nested,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        Authenticate(_ownerId);
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{draftId}}" } }
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
    public async Task Reviewer_cannot_submit()
    {
        AuthenticateRole(UserRole.Reviewer, Guid.NewGuid());

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_readyDraftId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("AUTH_NOT_AUTHORIZED", document.RootElement.GetProperty("errors").ToString(), StringComparison.Ordinal);
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
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id } }",
                  "variables": { "input": { "id": "{{_readyDraftId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

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
