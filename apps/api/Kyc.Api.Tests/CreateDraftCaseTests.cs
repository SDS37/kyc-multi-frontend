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

public sealed class CreateDraftCaseTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;

    public CreateDraftCaseTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Draft Co",
            Slug = $"draft-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@draft.example",
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
    public async Task Customer_creates_draft_with_status_Draft_and_empty_FormData()
    {
        AuthenticateCustomer();

        var payload = await PostGraphqlAsync(
            """
            {
              "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id title status formData tenantId customerUserId } }",
              "variables": { "input": { "title": "Onboarding ACME" } }
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var created = payload.GetProperty("data").GetProperty("createDraftCase");
        Assert.Equal("Onboarding ACME", created.GetProperty("title").GetString());
        Assert.Equal("DRAFT", created.GetProperty("status").GetString());
        Assert.Equal("{}", created.GetProperty("formData").GetString());
        Assert.Equal(_tenantId, created.GetProperty("tenantId").GetGuid());
        Assert.Equal(_customerId, created.GetProperty("customerUserId").GetGuid());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = created.GetProperty("id").GetGuid();
        // No HTTP JWT on this scope — bypass filter to assert persistence.
        var row = await db.Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        Assert.Equal(CaseStatus.Draft, row.Status);
        Assert.Equal(_tenantId, row.TenantId);
        Assert.Equal(_customerId, row.CustomerUserId);
        Assert.Equal("{}", row.FormData);
    }

    [Fact]
    public async Task Oversized_formData_returns_VALIDATION()
    {
        AuthenticateCustomer();
        var oversized = $$"""{"pad":"{{new string('x', CreateDraftCaseService.MaxFormDataUtf8Bytes)}}"}""";

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id } }",
                  "variables": { "input": { "title": "Too big", "formData": {{JsonSerializer.Serialize(oversized)}} } }
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
        AuthenticateCustomer();
        var nested = "1";
        for (var i = 0; i < CreateDraftCaseService.MaxFormDataDepth + 2; i++)
        {
            nested = $$"""{"a":{{nested}}}""";
        }

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id } }",
                  "variables": { "input": { "title": "Too deep", "formData": {{JsonSerializer.Serialize(nested)}} } }
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
    public async Task Title_is_required()
    {
        AuthenticateCustomer();

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                """{"query":"mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id } }","variables":{"input":{"title":"  "}}}""",
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Contains("VALIDATION", errors.ToString(), StringComparison.Ordinal);
        Assert.Contains("Title is required", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TenantId_comes_from_JWT_not_from_client_fields()
    {
        AuthenticateCustomer();

        // Schema must not accept tenantId / customerUserId on input (ADR-007).
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                """
                {
                  "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { tenantId customerUserId } }",
                  "variables": {
                    "input": {
                      "title": "Should ignore forged ids",
                      "tenantId": "11111111-1111-1111-1111-111111111111",
                      "customerUserId": "22222222-2222-2222-2222-222222222222"
                    }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        // Hot Chocolate rejects unknown input fields — or if somehow accepted, response still uses JWT.
        if (root.TryGetProperty("errors", out var errors))
        {
            Assert.Contains("tenantId", errors.ToString(), StringComparison.OrdinalIgnoreCase);
            return;
        }

        var created = root.GetProperty("data").GetProperty("createDraftCase");
        Assert.Equal(_tenantId, created.GetProperty("tenantId").GetGuid());
        Assert.Equal(_customerId, created.GetProperty("customerUserId").GetGuid());
    }

    [Fact]
    public async Task Reviewer_cannot_create_draft_case()
    {
        Authenticate(UserRole.Reviewer, Guid.NewGuid(), Guid.NewGuid());

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

    [Fact]
    public async Task Stale_customer_JWT_returns_AUTH_FAILED()
    {
        // Customer role token whose sub is not in the database.
        Authenticate(UserRole.Customer, _tenantId, Guid.NewGuid());

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                """{"query":"mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id } }","variables":{"input":{"title":"Stale"}}}""",
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Contains("AUTH_FAILED", errors.ToString(), StringComparison.Ordinal);
        Assert.Contains(CreateDraftCaseService.GenericAuthFailure, errors.ToString(), StringComparison.Ordinal);
    }

    private void AuthenticateCustomer() => Authenticate(UserRole.Customer, _tenantId, _customerId);

    private void Authenticate(UserRole role, Guid tenantId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
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
