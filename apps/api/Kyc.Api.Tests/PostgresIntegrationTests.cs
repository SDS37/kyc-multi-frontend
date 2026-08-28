using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class PostgresIntegrationTests : IClassFixture<PostgresApiFactory>, IAsyncLifetime
{
    private readonly PostgresApiFactory _factory;
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;

    public PostgresIntegrationTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KYC_TEST_POSTGRES")))
        {
            return;
        }

        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Pg Co",
            Slug = $"pg-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@pg.example",
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

    [PostgresFact]
    public async Task Ready_is_healthy_against_live_postgres()
    {
        using var response = await _client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task Jsonb_formData_round_trips()
    {
        AuthenticateCustomer();
        var form = """{"fullName":"Ada","step":1}""";

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id formData } }",
                  "variables": { "input": { "title": "PG draft", "formData": {{JsonSerializer.Serialize(form)}} } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("errors", out _), root.ToString());
        var id = root.GetProperty("data").GetProperty("createDraftCase").GetProperty("id").GetGuid();
        using var fromApi = JsonDocument.Parse(
            root.GetProperty("data").GetProperty("createDraftCase").GetProperty("formData").GetString()!);
        Assert.Equal("Ada", fromApi.RootElement.GetProperty("fullName").GetString());
        Assert.Equal(1, fromApi.RootElement.GetProperty("step").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        using var fromDb = JsonDocument.Parse(row.FormData);
        Assert.Equal("Ada", fromDb.RootElement.GetProperty("fullName").GetString());
        Assert.Equal(1, fromDb.RootElement.GetProperty("step").GetInt32());
        Assert.Equal(CaseStatus.Draft, row.Status);
    }

    [PostgresFact]
    public async Task Duplicate_tenant_slug_is_rejected()
    {
        var slug = $"dup-{Guid.NewGuid():N}"[..16];
        var first = await RegisterTenantAsync(slug);
        Assert.False(first.TryGetProperty("errors", out _), first.ToString());

        var second = await RegisterTenantAsync(slug);
        Assert.True(second.TryGetProperty("errors", out var errors), second.ToString());
        Assert.Contains("already taken", errors.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<JsonElement> RegisterTenantAsync(string slug)
    {
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug } }",
                  "variables": {
                    "input": {
                      "tenantName": "Dup Co",
                      "tenantSlug": "{{slug}}",
                      "adminEmail": "a@{{slug}}.example",
                      "adminPassword": "ChangeMe1"
                    }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private void AuthenticateCustomer()
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@pg.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
