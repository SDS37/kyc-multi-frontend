using Kyc.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

/// <summary>
/// Skips when <c>KYC_TEST_POSTGRES</c> is unset so laptops keep the SQLite suite (KYC-108).
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KYC_TEST_POSTGRES")))
        {
            Skip = "Set KYC_TEST_POSTGRES to run against live Postgres (api-ci).";
        }
    }
}

/// <summary>
/// Real Npgsql host + EF migrations. Used only when <c>KYC_TEST_POSTGRES</c> is set.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("KYC_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = Environment.GetEnvironmentVariable("KYC_TEST_POSTGRES")
            ?? "Host=127.0.0.1;Port=1;Database=unused;Username=x;Password=x";

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", connection);
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-at-least-32-chars!!");
        builder.UseSetting("Jwt:Issuer", "kyc-test");
        builder.UseSetting("Jwt:Audience", "kyc-test");
        builder.UseSetting("Jwt:ExpiresMinutes", "60");
        builder.UseSetting("ObjectStorage:Provider", "InMemory");
        builder.UseSetting("ObjectStorage:BucketName", "kyc-documents");
        builder.UseSetting("Registration:AllowPublicRegistration", "true");
    }
}
