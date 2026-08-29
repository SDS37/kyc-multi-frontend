using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kyc.Api.Tests;

public sealed class CountingPasswordHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> _inner = new();

    public int VerifyCount { get; private set; }

    public string HashPassword(User user, string password) => _inner.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        VerifyCount++;
        return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}

public sealed class LoginTimingApiFactory : ApiFactory
{
    public CountingPasswordHasher Hasher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordHasher<User>>();
            services.AddSingleton<IPasswordHasher<User>>(Hasher);
        });
    }
}

public sealed class LoginTimingTests : IClassFixture<LoginTimingApiFactory>
{
    private readonly LoginTimingApiFactory _factory;

    public LoginTimingTests(LoginTimingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unknown_user_still_verifies_a_password_hash()
    {
        using var client = _factory.CreateClient();
        var slug = await RegisterAsync(client, "miss-user");
        var before = _factory.Hasher.VerifyCount;

        var payload = await PostGraphqlAsync(client, LoginBody(slug, "nobody@miss.example", "ChangeMe1"));
        Assert.Contains("AUTH_FAILED", payload.GetProperty("errors").ToString(), StringComparison.Ordinal);
        Assert.True(_factory.Hasher.VerifyCount > before, "missing user must still call VerifyHashedPassword");
    }

    [Fact]
    public async Task Unknown_tenant_still_verifies_a_password_hash()
    {
        using var client = _factory.CreateClient();
        var before = _factory.Hasher.VerifyCount;

        var payload = await PostGraphqlAsync(
            client,
            LoginBody("no-such-tenant", "a@example.com", "ChangeMe1"));
        Assert.Contains("AUTH_FAILED", payload.GetProperty("errors").ToString(), StringComparison.Ordinal);
        Assert.True(_factory.Hasher.VerifyCount > before, "missing tenant must still call VerifyHashedPassword");
    }

    [Fact]
    public async Task Inactive_tenant_still_verifies_a_password_hash()
    {
        using var client = _factory.CreateClient();
        var slug = await RegisterAsync(client, "inactive");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = await db.Tenants.SingleAsync(t => t.Slug == slug);
            tenant.IsActive = false;
            await db.SaveChangesAsync();
        }

        var before = _factory.Hasher.VerifyCount;
        var payload = await PostGraphqlAsync(client, LoginBody(slug, $"a@{slug}.example", "ChangeMe1"));
        Assert.Contains("AUTH_FAILED", payload.GetProperty("errors").ToString(), StringComparison.Ordinal);
        Assert.True(_factory.Hasher.VerifyCount > before, "inactive tenant must still call VerifyHashedPassword");
    }

    [Fact]
    public async Task Oversized_password_returns_VALIDATION_without_verify()
    {
        using var client = _factory.CreateClient();
        var before = _factory.Hasher.VerifyCount;
        var oversized = new string('x', LoginService.MaxPasswordLength + 1);

        var payload = await PostGraphqlAsync(
            client,
            LoginBody("any-tenant", "a@example.com", oversized));
        var errors = payload.GetProperty("errors").ToString();
        Assert.Contains("VALIDATION", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("AUTH_FAILED", errors, StringComparison.Ordinal);
        Assert.Contains($"{LoginService.MaxPasswordLength}", errors, StringComparison.Ordinal);
        Assert.Equal(before, _factory.Hasher.VerifyCount);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string prefix)
    {
        var slug = $"{prefix}-{Guid.NewGuid():N}"[..16];
        var register = await PostGraphqlAsync(client, $$"""
            {
              "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug } }",
              "variables": {
                "input": {
                  "tenantName": "Login Co",
                  "tenantSlug": "{{slug}}",
                  "adminEmail": "a@{{slug}}.example",
                  "adminPassword": "ChangeMe1"
                }
              }
            }
            """);
        Assert.False(register.TryGetProperty("errors", out _), register.ToString());
        return slug;
    }

    private static string LoginBody(string slug, string email, string password) =>
        $$"""
        {
          "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken } }",
          "variables": {
            "input": {
              "tenantSlug": "{{slug}}",
              "email": "{{email}}",
              "password": "{{password}}"
            }
          }
        }
        """;

    private static async Task<JsonElement> PostGraphqlAsync(HttpClient client, string jsonBody)
    {
        using var response = await client.PostAsync(
            "/graphql",
            new StringContent(jsonBody, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
