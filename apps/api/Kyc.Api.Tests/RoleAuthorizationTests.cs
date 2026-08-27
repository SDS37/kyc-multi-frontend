using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class RoleAuthorizationTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private HttpClient _client = null!;

    public RoleAuthorizationTests(ApiFactory factory)
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
    public async Task Reviewer_can_call_reviewerOnlyPing()
    {
        Authenticate(UserRole.Reviewer);
        var payload = await PostGraphqlAsync("""{ "query": "mutation { reviewerOnlyPing }" }""");
        Assert.Equal("reviewer-ok", payload.GetProperty("data").GetProperty("reviewerOnlyPing").GetString());
    }

    [Fact]
    public async Task Customer_cannot_call_reviewerOnlyPing()
    {
        Authenticate(UserRole.Customer);
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent("""{"query":"mutation { reviewerOnlyPing }"}""", Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Contains("AUTH_NOT_AUTHORIZED", errors.ToString(), StringComparison.Ordinal);
        Assert.False(
            root.TryGetProperty("data", out var data) &&
            data.ValueKind != JsonValueKind.Null &&
            data.TryGetProperty("reviewerOnlyPing", out var value) &&
            value.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task Customer_can_call_customerOnlyPing()
    {
        Authenticate(UserRole.Customer);
        var payload = await PostGraphqlAsync("""{ "query": "mutation { customerOnlyPing }" }""");
        Assert.Equal("customer-ok", payload.GetProperty("data").GetProperty("customerOnlyPing").GetString());
    }

    [Fact]
    public async Task Reviewer_cannot_call_customerOnlyPing()
    {
        Authenticate(UserRole.Reviewer);
        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent("""{"query":"mutation { customerOnlyPing }"}""", Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Contains("AUTH_NOT_AUTHORIZED", errors.ToString(), StringComparison.Ordinal);
    }

    private void Authenticate(UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = $"{role.ToString().ToLowerInvariant()}@example.com",
            PasswordHash = "unused",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
