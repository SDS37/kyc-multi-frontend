using System.Net;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Microsoft.AspNetCore.Hosting;

namespace Kyc.Api.Tests;

/// <summary>
/// Default production shape: public registration off (ApiFactory turns it on for other tests).
/// </summary>
public sealed class RegistrationDisabledFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Registration:AllowPublicRegistration", "false");
    }
}

public sealed class RegistrationDisabledTests(RegistrationDisabledFactory factory)
    : IClassFixture<RegistrationDisabledFactory>
{
    [Fact]
    public async Task GraphQl_registerTenant_is_rejected_when_public_registration_is_off()
    {
        using var client = factory.CreateClient();
        var slug = $"off-{Guid.NewGuid():N}"[..16];
        using var content = new StringContent(
            $$"""
            {
              "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug } }",
              "variables": {
                "input": {
                  "tenantName": "Off Co",
                  "tenantSlug": "{{slug}}",
                  "adminEmail": "a@{{slug}}.example",
                  "adminPassword": "ChangeMe1234"
                }
              }
            }
            """,
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/graphql", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        var payload = document.RootElement;
        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains(RegisterTenantService.RegistrationDisabledMessage, errors.ToString(), StringComparison.Ordinal);
        Assert.Contains("VALIDATION", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rest_register_tenant_is_rejected_when_public_registration_is_off()
    {
        using var client = factory.CreateClient();
        var slug = $"offr-{Guid.NewGuid():N}"[..16];
        using var content = new StringContent(
            $$"""
            {
              "tenantName": "Off Co",
              "tenantSlug": "{{slug}}",
              "adminEmail": "a@{{slug}}.example",
              "adminPassword": "ChangeMe1234"
            }
            """,
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/register-tenant", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(RegisterTenantService.RegistrationDisabledMessage, body, StringComparison.Ordinal);
    }
}
