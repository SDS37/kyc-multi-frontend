using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Kyc.Api.Tests;

public sealed class ObservabilityApiFactory : ApiFactory
{
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(Logs);
            logging.SetMinimumLevel(LogLevel.Information);
        });
    }
}

public sealed class ObservabilityTests(ObservabilityApiFactory factory) : IClassFixture<ObservabilityApiFactory>
{
    private const string SecretPassword = "SuperSecret-DoNotLog";

    [Fact]
    public async Task Health_echoes_generated_request_id()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(RequestCorrelationMiddleware.HeaderName, out var values));
        var id = Assert.Single(values);
        Assert.False(string.IsNullOrWhiteSpace(id));
    }

    [Fact]
    public async Task Health_echoes_safe_incoming_request_id()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            RequestCorrelationMiddleware.HeaderName,
            "local-debug-1");

        using var response = await client.GetAsync("/health");
        Assert.Equal("local-debug-1", Assert.Single(
            response.Headers.GetValues(RequestCorrelationMiddleware.HeaderName)));
    }

    [Fact]
    public async Task Health_rejects_unsafe_incoming_request_id()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            RequestCorrelationMiddleware.HeaderName,
            "not a valid id!!");

        using var response = await client.GetAsync("/health");
        var echoed = Assert.Single(response.Headers.GetValues(RequestCorrelationMiddleware.HeaderName));
        Assert.NotEqual("not a valid id!!", echoed);
        Assert.False(string.IsNullOrWhiteSpace(echoed));
    }

    [Fact]
    public async Task Health_probe_is_not_logged_at_information()
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        Assert.DoesNotContain(
            factory.Logs.Entries,
            e => e.Message.Contains("HTTP GET /health", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Http_request_log_includes_request_id_scope()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var httpLog = factory.Logs.Entries.FirstOrDefault(e =>
            e.Message.Contains("HTTP GET /ready", StringComparison.Ordinal));
        Assert.NotNull(httpLog);
        Assert.True(HasRequestIdScope(httpLog.Scopes), "RequestId should be in the log scope.");
    }

    [Fact]
    public async Task Ready_failure_is_logged_without_connection_secrets()
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/ready");

        var joined = JoinLogs();
        Assert.Contains(PostgresReadyHealthCheck.UnreachableLog, joined, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Username=x", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("Database=unused", joined, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_failure_is_logged_without_password()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(
            $$"""{"tenantSlug":"nope","email":"a@b.example","password":"{{SecretPassword}}"}""",
            Encoding.UTF8,
            "application/json");
        using var response = await client.PostAsync("/api/login", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var joined = JoinLogs();
        Assert.Contains(LoginService.RejectedLog, joined, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretPassword, joined, StringComparison.Ordinal);
        Assert.DoesNotContain("a@b.example", joined, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphQl_auth_failure_logs_code_without_token()
    {
        const string leakedToken = "eyJhbGciOi-not-a-real-token";
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", leakedToken);

        using var content = new StringContent(
            """{ "query": "query { apiStatus }" }""",
            Encoding.UTF8,
            "application/json");
        using var response = await client.PostAsync("/graphql", content);
        var body = await response.Content.ReadAsStringAsync();
        var joined = JoinLogs();

        Assert.DoesNotContain(leakedToken, joined, StringComparison.Ordinal);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.Contains("JWT authentication failed", joined, StringComparison.Ordinal);
            return;
        }

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.Contains("AUTH_NOT_AUTHENTICATED", errors.ToString(), StringComparison.Ordinal);
        Assert.Contains("AUTH_NOT_AUTHENTICATED", joined, StringComparison.Ordinal);
    }

    private string JoinLogs() =>
        string.Join('\n', factory.Logs.Entries.Select(e => $"{e.Category} {e.Message}"));

    private static bool HasRequestIdScope(object[] scopes)
    {
        foreach (var scope in scopes)
        {
            if (scope is IEnumerable<KeyValuePair<string, object>> pairs
                && pairs.Any(p => p.Key == RequestCorrelationMiddleware.LogScopeKey
                                  && p.Value is string { Length: > 0 }))
            {
                return true;
            }
        }

        return false;
    }
}
