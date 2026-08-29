using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Kyc.Api.Tests;

/// <summary>
/// Production host (KYC-105): introspection and SDL off. SQLite still swapped in via <see cref="ApiFactory"/>.
/// </summary>
public sealed class ProductionApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
    }
}

public sealed class GraphQlHostHardeningTests(ApiFactory development, ProductionApiFactory production) : IClassFixture<ApiFactory>, IClassFixture<ProductionApiFactory>
{
    private const string IntrospectionQuery = """{ "query": "query { __schema { queryType { name } } }" }""";

    [Fact]
    public async Task Development_allows_authenticated_introspection()
    {
        using var client = development.CreateClient();
        var token = await RegisterAndLoginAsync(client, "dev-intro");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = await PostGraphqlAsync(client, IntrospectionQuery);
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal("Query", payload.GetProperty("data").GetProperty("__schema").GetProperty("queryType").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Production_rejects_authenticated_introspection()
    {
        using var client = production.CreateClient();
        var token = await RegisterAndLoginAsync(client, "prod-intro");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsync(
            "/graphql",
            new StringContent(IntrospectionQuery, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"HTTP {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        var payload = document.RootElement;
        Assert.False(
            payload.TryGetProperty("data", out var data) &&
            data.ValueKind != JsonValueKind.Null &&
            data.TryGetProperty("__schema", out _),
            payload.ToString());
        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("HC0046", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_serves_schema_sdl()
    {
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/graphql?sdl");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("type Query", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_does_not_serve_schema_sdl()
    {
        using var client = production.CreateClient();
        using var response = await client.GetAsync("/graphql?sdl");
        var body = await response.Content.ReadAsStringAsync();
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("type Query", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_apiStatus_still_works_under_depth_limit()
    {
        using var client = development.CreateClient();
        var token = await RegisterAndLoginAsync(client, "dev-depth");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = await PostGraphqlAsync(client, """{ "query": "query { apiStatus }" }""");
        Assert.Equal("ok", payload.GetProperty("data").GetProperty("apiStatus").GetString());
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string prefix)
    {
        var slug = $"{prefix}-{Guid.NewGuid():N}"[..16];
        var register = await PostGraphqlAsync(client, $$"""
            {
              "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantId } }",
              "variables": {
                "input": {
                  "tenantName": "Host Co",
                  "tenantSlug": "{{slug}}",
                  "adminEmail": "a@{{slug}}.example",
                  "adminPassword": "ChangeMe1"
                }
              }
            }
            """);
        Assert.True(register.TryGetProperty("data", out _), register.ToString());

        var login = await PostGraphqlAsync(client, $$"""
            {
              "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken } }",
              "variables": {
                "input": {
                  "tenantSlug": "{{slug}}",
                  "email": "a@{{slug}}.example",
                  "password": "ChangeMe1"
                }
              }
            }
            """);

        return login.GetProperty("data").GetProperty("login").GetProperty("accessToken").GetString()!;
    }

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
