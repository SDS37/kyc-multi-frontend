using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Microsoft.AspNetCore.Hosting;

namespace Kyc.Api.Tests;

public sealed class CaptchaRequiredFactory : ApiFactory
{
    public const string BypassToken = "kyc-test-captcha";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Captcha:RequiredForRegister", "true");
        builder.UseSetting("Captcha:Provider", CaptchaOptions.TestProvider);
        builder.UseSetting("Captcha:BypassToken", BypassToken);
    }
}

public sealed class CaptchaTests(CaptchaRequiredFactory factory) : IClassFixture<CaptchaRequiredFactory>
{
    [Fact]
    public async Task Register_without_captcha_is_validation()
    {
        using var client = factory.CreateClient();
        var payload = await PostRegisterAsync(client, UniqueSlug("cap"), captchaToken: null);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("VALIDATION", errors.ToString(), StringComparison.Ordinal);
        Assert.Contains(CaptchaMessages.Required, errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_with_wrong_captcha_is_validation()
    {
        using var client = factory.CreateClient();
        var payload = await PostRegisterAsync(client, UniqueSlug("capw"), captchaToken: "nope");

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("VALIDATION", errors.ToString(), StringComparison.Ordinal);
        Assert.Contains(CaptchaMessages.Failed, errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_with_test_captcha_succeeds()
    {
        using var client = factory.CreateClient();
        var payload = await PostRegisterAsync(
            client,
            UniqueSlug("capok"),
            captchaToken: CaptchaRequiredFactory.BypassToken);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.True(payload.GetProperty("data").TryGetProperty("registerTenant", out _), payload.ToString());
    }

    private static string UniqueSlug(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16];

    private static async Task<JsonElement> PostRegisterAsync(
        HttpClient client,
        string slug,
        string? captchaToken)
    {
        var captchaJson = captchaToken is null
            ? ""
            : $""", "captchaToken": {JsonSerializer.Serialize(captchaToken)}""";
        using var response = await client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug } }",
                  "variables": {
                    "input": {
                      "tenantName": "Captcha Co",
                      "tenantSlug": "{{slug}}",
                      "adminEmail": "a@{{slug}}.example",
                      "adminPassword": "ChangeMe1234"{{captchaJson}}
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
}
