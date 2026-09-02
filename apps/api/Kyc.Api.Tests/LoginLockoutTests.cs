using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Microsoft.AspNetCore.Hosting;

namespace Kyc.Api.Tests;

public sealed class LockoutApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Lockout:MaxFailedAttempts", "2");
        builder.UseSetting("Lockout:DurationMinutes", "15");
    }
}

public sealed class LoginLockoutTests(LockoutApiFactory factory) : IClassFixture<LockoutApiFactory>
{
    [Fact]
    public async Task Locked_account_still_returns_generic_auth_failure()
    {
        using var client = factory.CreateClient();
        var slug = await RegisterAsync(client, "lock");
        var email = $"a@{slug}.example";
        var wrong = LoginBody(slug, email, "WrongPassword1");

        var first = await PostGraphqlAsync(client, wrong);
        var second = await PostGraphqlAsync(client, wrong);
        var lockedWrong = await PostGraphqlAsync(client, wrong);
        var lockedRight = await PostGraphqlAsync(client, LoginBody(slug, email, "ChangeMe1234"));

        Assert.Contains("AUTH_FAILED", first.GetProperty("errors").ToString(), StringComparison.Ordinal);
        Assert.Contains("AUTH_FAILED", second.GetProperty("errors").ToString(), StringComparison.Ordinal);
        Assert.Contains("AUTH_FAILED", lockedWrong.GetProperty("errors").ToString(), StringComparison.Ordinal);
        Assert.Contains("AUTH_FAILED", lockedRight.GetProperty("errors").ToString(), StringComparison.Ordinal);
        Assert.Contains(LoginService.GenericAuthFailure, lockedRight.GetProperty("errors").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("lock", lockedRight.GetProperty("errors").ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string prefix)
    {
        var slug = $"{prefix}-{Guid.NewGuid():N}"[..16];
        var payload = await PostGraphqlAsync(client, $$"""
            {
              "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug } }",
              "variables": {
                "input": {
                  "tenantName": "Lock Co",
                  "tenantSlug": "{{slug}}",
                  "adminEmail": "a@{{slug}}.example",
                  "adminPassword": "ChangeMe1234"
                }
              }
            }
            """);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
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
        using var response = await PostGraphqlRaw(client, jsonBody);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static Task<HttpResponseMessage> PostGraphqlRaw(HttpClient client, string jsonBody) =>
        client.PostAsync("/graphql", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
}
