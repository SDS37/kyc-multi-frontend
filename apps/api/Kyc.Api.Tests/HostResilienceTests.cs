using System.Net;
using Kyc.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

/// <summary>
/// Hosts the API with the real Npgsql provider (no SQLite swap) so readiness and EF retry
/// configuration can be asserted. Connection is intentionally unreachable.
/// </summary>
public sealed class NpgsqlHostFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "ConnectionStrings:Postgres",
            "Host=127.0.0.1;Port=1;Database=unused;Username=x;Password=x");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-at-least-32-chars!!");
        builder.UseSetting("Jwt:Issuer", "kyc-test");
        builder.UseSetting("Jwt:Audience", "kyc-test");
        builder.UseSetting("Jwt:ExpiresMinutes", "60");
        builder.UseSetting("ObjectStorage:Provider", "InMemory");
        builder.UseSetting("ObjectStorage:BucketName", "kyc-documents");
    }
}

public sealed class HostResilienceTests : IClassFixture<NpgsqlHostFactory>, IAsyncLifetime
{
    private readonly NpgsqlHostFactory _factory;
    private HttpClient _client = null!;

    public HostResilienceTests(NpgsqlHostFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Health_stays_healthy_when_postgres_is_unreachable()
    {
        using var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_fails_when_postgres_is_unreachable()
    {
        using var response = await _client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_is_anonymous()
    {
        using var response = await _client.GetAsync("/ready");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Ef_npgsql_retries_transient_failures()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.Database.CreateExecutionStrategy().RetriesOnFailure);
    }
}
