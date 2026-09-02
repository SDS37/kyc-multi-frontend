using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class InviteOnlyFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Registration:AllowPublicRegistration", "false");
    }
}

public sealed class PublicInviteRequiredFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Registration:AllowPublicRegistration", "true");
        builder.UseSetting("Registration:RequireInviteCode", "true");
    }
}

public sealed class InviteRegistrationTests
{
    [Fact]
    public async Task Closed_registration_accepts_a_fresh_invite()
    {
        await using var factory = new InviteOnlyFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var code = await SeedInviteAsync(factory);

        var payload = await PostRegisterAsync(client, UniqueSlug("inv"), code);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.True(payload.GetProperty("data").TryGetProperty("registerTenant", out _), payload.ToString());
    }

    [Fact]
    public async Task Closed_registration_rejects_missing_and_spent_invites_without_leaking()
    {
        await using var factory = new InviteOnlyFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var code = await SeedInviteAsync(factory);
        var slug = UniqueSlug("inv2");

        var missing = await PostRegisterAsync(client, UniqueSlug("invm"), inviteCode: null);
        Assert.Contains(
            RegisterTenantService.RegistrationDisabledMessage,
            missing.GetProperty("errors").ToString(),
            StringComparison.Ordinal);

        var first = await PostRegisterAsync(client, slug, code);
        Assert.False(first.TryGetProperty("errors", out _), first.ToString());

        var spent = await PostRegisterAsync(client, UniqueSlug("invs"), code);
        Assert.Contains(
            RegisterTenantService.RegistrationDisabledMessage,
            spent.GetProperty("errors").ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_registration_can_still_require_invites()
    {
        await using var factory = new PublicInviteRequiredFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var missing = await PostRegisterAsync(client, UniqueSlug("pubm"), inviteCode: null);
        Assert.Contains(
            RegisterTenantService.InviteRequiredMessage,
            missing.GetProperty("errors").ToString(),
            StringComparison.Ordinal);

        var invalid = await PostRegisterAsync(client, UniqueSlug("pubi"), inviteCode: "ffffffffffffffff");
        Assert.Contains(
            RegisterTenantService.GenericRegisterFailure,
            invalid.GetProperty("errors").ToString(),
            StringComparison.Ordinal);

        var code = await SeedInviteAsync(factory);
        var ok = await PostRegisterAsync(client, UniqueSlug("pubok"), code);
        Assert.False(ok.TryGetProperty("errors", out _), ok.ToString());
    }

    [Fact]
    public async Task Expired_invite_cannot_be_redeemed()
    {
        await using var factory = new InviteOnlyFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var code = await SeedInviteAsync(factory, DateTimeOffset.UtcNow.AddMinutes(-1));

        var payload = await PostRegisterAsync(client, UniqueSlug("exp"), code);

        Assert.Contains(
            RegisterTenantService.RegistrationDisabledMessage,
            payload.GetProperty("errors").ToString(),
            StringComparison.Ordinal);
    }

    private static async Task<string> SeedInviteAsync(ApiFactory factory, DateTimeOffset? expiresAt = null)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.RegistrationInvites.Add(new RegistrationInvite
        {
            Id = Guid.NewGuid(),
            CodeHash = InviteCodeHasher.Hash(code),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync();
        return code;
    }

    private static string UniqueSlug(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16];

    private static async Task<JsonElement> PostRegisterAsync(
        HttpClient client,
        string slug,
        string? inviteCode)
    {
        var inviteJson = inviteCode is null
            ? ""
            : $""", "inviteCode": {JsonSerializer.Serialize(inviteCode)}""";
        using var response = await client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug } }",
                  "variables": {
                    "input": {
                      "tenantName": "Invite Co",
                      "tenantSlug": "{{slug}}",
                      "adminEmail": "a@{{slug}}.example",
                      "adminPassword": "ChangeMe1234"{{inviteJson}}
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
