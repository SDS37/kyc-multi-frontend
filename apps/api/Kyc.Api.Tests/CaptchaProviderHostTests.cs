using Kyc.Api.Application.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kyc.Api.Tests;

public sealed class ProductionCaptchaTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:Postgres",
            "Host=127.0.0.1;Port=1;Database=unused;Username=x;Password=x");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-at-least-32-chars!!");
        builder.UseSetting("Jwt:Issuer", "kyc-test");
        builder.UseSetting("Jwt:Audience", "kyc-test");
        builder.UseSetting("ObjectStorage:Provider", "InMemory");
        builder.UseSetting("ObjectStorage:AllowInMemoryOutsideDevelopment", "true");
        builder.UseSetting("Captcha:Provider", CaptchaOptions.TestProvider);
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("Registration:AllowInProduction", "true");
    }
}

public sealed class CaptchaProviderHostTests
{
    [Fact]
    public void Production_rejects_captcha_test_provider()
    {
        using var factory = new ProductionCaptchaTestFactory();
        var ex = Record.Exception(() => factory.CreateClient());
        Assert.NotNull(ex);
        Assert.Contains("Captcha:Provider", ex.ToString(), StringComparison.Ordinal);
        Assert.Contains("test", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
