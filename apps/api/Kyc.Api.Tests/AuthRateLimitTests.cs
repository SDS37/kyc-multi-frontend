using System.Net;
using System.Text;
using Kyc.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Kyc.Api.Tests;

public sealed class TightLoginLimitFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("AuthLimits:LoginPermitPerMinute", "2");
        builder.UseSetting("AuthLimits:RegisterPermitPerMinute", "20");
        builder.UseSetting("AuthLimits:GraphqlPermitPerMinute", "100");
    }
}

public sealed class TightGraphqlLimitFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("AuthLimits:LoginPermitPerMinute", "100");
        builder.UseSetting("AuthLimits:RegisterPermitPerMinute", "20");
        builder.UseSetting("AuthLimits:GraphqlPermitPerMinute", "2");
    }
}

public sealed class TightRegisterLimitFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("AuthLimits:LoginPermitPerMinute", "100");
        builder.UseSetting("AuthLimits:RegisterPermitPerMinute", "2");
        builder.UseSetting("AuthLimits:GraphqlPermitPerMinute", "100");
    }
}

public sealed class AuthRateLimitTests
{
    [Fact]
    public async Task GraphQl_login_over_limit_returns_generic_429()
    {
        await using var factory = new TightLoginLimitFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var body = LoginBody("no-such-tenant", "a@example.com");

        using var first = await PostGraphqlAsync(client, body);
        using var second = await PostGraphqlAsync(client, body);
        using var third = await PostGraphqlAsync(client, body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        var thirdBody = await third.Content.ReadAsStringAsync();
        Assert.Contains(AuthRateLimiting.TooManyRequestsMessage, thirdBody, StringComparison.Ordinal);
        Assert.DoesNotContain("a@example.com", thirdBody, StringComparison.Ordinal);
        Assert.DoesNotContain("AUTH_FAILED", thirdBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rest_login_over_limit_returns_generic_429()
    {
        await using var factory = new TightLoginLimitFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        const string json = """{"tenantSlug":"no-such-tenant","email":"a@example.com","password":"ChangeMe1234"}""";

        using var first = await client.PostAsync("/api/login", Json(json));
        using var second = await client.PostAsync("/api/login", Json(json));
        using var third = await client.PostAsync("/api/login", Json(json));

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        var thirdBody = await third.Content.ReadAsStringAsync();
        Assert.Contains(AuthRateLimiting.TooManyRequestsMessage, thirdBody, StringComparison.Ordinal);
        Assert.DoesNotContain("a@example.com", thirdBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rest_register_over_limit_returns_generic_429()
    {
        await using var factory = new TightRegisterLimitFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        using var first = await client.PostAsync("/api/register-tenant", RegisterJson("r1"));
        using var second = await client.PostAsync("/api/register-tenant", RegisterJson("r2"));
        using var third = await client.PostAsync("/api/register-tenant", RegisterJson("r3"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        var thirdBody = await third.Content.ReadAsStringAsync();
        Assert.Contains(AuthRateLimiting.TooManyRequestsMessage, thirdBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphQl_register_over_limit_returns_generic_429()
    {
        await using var factory = new TightRegisterLimitFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        using var first = await PostGraphqlAsync(client, RegisterGraphqlBody("g1"));
        using var second = await PostGraphqlAsync(client, RegisterGraphqlBody("g2"));
        using var third = await PostGraphqlAsync(client, RegisterGraphqlBody("g3"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        var thirdBody = await third.Content.ReadAsStringAsync();
        Assert.Contains(AuthRateLimiting.TooManyRequestsMessage, thirdBody, StringComparison.Ordinal);
        Assert.DoesNotContain("registerTenant", thirdBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Graphql_other_operations_do_not_consume_login_bucket()
    {
        await using var factory = new TightGraphqlLimitFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        const string status = """{ "query": "query { apiStatus }" }""";

        using var first = await PostGraphqlAsync(client, status);
        using var second = await PostGraphqlAsync(client, status);
        using var third = await PostGraphqlAsync(client, status);
        using var login = await PostGraphqlAsync(client, LoginBody("no-such-tenant", "a@example.com"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.Contains("AUTH_FAILED", loginBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aliased_double_login_returns_generic_429()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        const string body = """
            {
              "query": "mutation { a: login(input: { tenantSlug: \"x\", email: \"a@example.com\", password: \"ChangeMe1234\" }) { accessToken } b: login(input: { tenantSlug: \"x\", email: \"a@example.com\", password: \"ChangeMe1234\" }) { accessToken } }"
            }
            """;

        using var response = await PostGraphqlAsync(client, body);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(AuthRateLimiting.TooManyRequestsMessage, text, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_batch_of_two_logins_returns_generic_429()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        const string body = """
            [
              { "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken } }", "variables": { "input": { "tenantSlug": "x", "email": "a@example.com", "password": "ChangeMe1234" } } },
              { "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken } }", "variables": { "input": { "tenantSlug": "x", "email": "a@example.com", "password": "ChangeMe1234" } } }
            ]
            """;

        using var response = await PostGraphqlAsync(client, body);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(AuthRateLimiting.TooManyRequestsMessage, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mixed_login_and_register_returns_generic_429()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        const string body = """
            {
              "query": "mutation { login(input: { tenantSlug: \"x\", email: \"a@example.com\", password: \"ChangeMe1234\" }) { accessToken } registerTenant(input: { tenantName: \"A\", tenantSlug: \"x\", adminEmail: \"a@x.example\", adminPassword: \"ChangeMe1234\" }) { tenantSlug } }"
            }
            """;

        using var response = await PostGraphqlAsync(client, body);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(AuthRateLimiting.TooManyRequestsMessage, text, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commented_login_consumes_login_bucket()
    {
        await using var factory = new TightLoginLimitFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        const string body = """
            {
              "query": "mutation { login # x\n(input: { tenantSlug: \"no-such-tenant\", email: \"a@example.com\", password: \"ChangeMe1234\" }) { accessToken } }"
            }
            """;

        using var first = await PostGraphqlAsync(client, body);
        using var second = await PostGraphqlAsync(client, body);
        using var third = await PostGraphqlAsync(client, body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        var thirdBody = await third.Content.ReadAsStringAsync();
        Assert.Contains(AuthRateLimiting.TooManyRequestsMessage, thirdBody, StringComparison.Ordinal);
    }

    private static StringContent Json(string jsonBody) =>
        new(jsonBody, Encoding.UTF8, "application/json");

    private static StringContent RegisterJson(string prefix)
    {
        var slug = $"{prefix}-{Guid.NewGuid():N}"[..16];
        return Json($$"""
            {
              "tenantName": "Rate Co",
              "tenantSlug": "{{slug}}",
              "adminEmail": "a@{{slug}}.example",
              "adminPassword": "ChangeMe1234"
            }
            """);
    }

    private static string RegisterGraphqlBody(string prefix)
    {
        var slug = $"{prefix}-{Guid.NewGuid():N}"[..16];
        return $$"""
            {
              "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug } }",
              "variables": {
                "input": {
                  "tenantName": "Rate Co",
                  "tenantSlug": "{{slug}}",
                  "adminEmail": "a@{{slug}}.example",
                  "adminPassword": "ChangeMe1234"
                }
              }
            }
            """;
    }

    private static string LoginBody(string slug, string email) =>
        $$"""
        {
          "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken } }",
          "variables": {
            "input": {
              "tenantSlug": "{{slug}}",
              "email": "{{email}}",
              "password": "ChangeMe1234"
            }
          }
        }
        """;

    private static Task<HttpResponseMessage> PostGraphqlAsync(HttpClient client, string jsonBody) =>
        client.PostAsync("/graphql", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
}
