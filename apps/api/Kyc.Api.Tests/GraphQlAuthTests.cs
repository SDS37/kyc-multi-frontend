using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kyc.Api.Tests;

public sealed class GraphQlAuthTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ApiStatus_without_token_is_rejected()
    {
        var payload = await PostGraphqlAsync("""{ "query": "query { apiStatus }" }""");

        Assert.False(payload.TryGetProperty("data", out var data) &&
                     data.ValueKind != JsonValueKind.Null &&
                     data.TryGetProperty("apiStatus", out _));
        Assert.True(payload.TryGetProperty("errors", out var errors));
        Assert.Contains("AUTH_NOT_AUTHENTICATED", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiStatus_with_valid_token_returns_ok()
    {
        var slug = $"auth-{Guid.NewGuid():N}"[..16];
        var token = await RegisterAndLoginAsync(slug);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var payload = await PostGraphqlAsync("""{ "query": "query { apiStatus }" }""");

        Assert.Equal("ok", payload.GetProperty("data").GetProperty("apiStatus").GetString());
    }

    [Fact]
    public async Task RegisterTenant_and_login_mutations_work_anonymously()
    {
        var slug = $"anon-{Guid.NewGuid():N}"[..16];
        var register = await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug adminEmail } }",
              "variables": {
                "input": {
                  "tenantName": "Anon Co",
                  "tenantSlug": "{{slug}}",
                  "adminEmail": "a@{{slug}}.example",
                  "adminPassword": "ChangeMe1234"
                }
              }
            }
            """);

        Assert.Equal(slug, register.GetProperty("data").GetProperty("registerTenant").GetProperty("tenantSlug").GetString());

        var login = await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken tokenType } }",
              "variables": {
                "input": {
                  "tenantSlug": "{{slug}}",
                  "email": "a@{{slug}}.example",
                  "password": "ChangeMe1234"
                }
              }
            }
            """);

        var loginData = login.GetProperty("data").GetProperty("login");
        Assert.Equal("Bearer", loginData.GetProperty("tokenType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(loginData.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Invalid_bearer_token_is_rejected()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent("""{"query":"query { apiStatus }"}""", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // JwtBearer may yield 401, or proceed unauthenticated so field auth returns AUTH_NOT_AUTHENTICATED.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }

        Assert.True(response.IsSuccessStatusCode, body);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Contains("AUTH_NOT_AUTHENTICATED", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_remains_anonymous()
    {
        using var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private async Task<string> RegisterAndLoginAsync(string slug)
    {
        var register = await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantId } }",
              "variables": {
                "input": {
                  "tenantName": "Auth Co",
                  "tenantSlug": "{{slug}}",
                  "adminEmail": "a@{{slug}}.example",
                  "adminPassword": "ChangeMe1234"
                }
              }
            }
            """);
        Assert.True(register.TryGetProperty("data", out _), register.ToString());

        var login = await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken } }",
              "variables": {
                "input": {
                  "tenantSlug": "{{slug}}",
                  "email": "a@{{slug}}.example",
                  "password": "ChangeMe1234"
                }
              }
            }
            """);

        return login.GetProperty("data").GetProperty("login").GetProperty("accessToken").GetString()!;
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
