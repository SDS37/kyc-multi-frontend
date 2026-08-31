using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

/// <summary>
/// Demotion / inactive-tenant must fail closed on reads (not only mutations).
/// </summary>
public sealed class CallerStaleAuthTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _reviewerId;
    private Guid _peerCustomerId;
    private Guid _peerCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _peerCustomerId = Guid.NewGuid();
        _peerCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Stale Auth Co",
            Slug = $"stale-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.AddRange(
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@stale.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = _peerCustomerId,
                TenantId = _tenantId,
                Email = "peer@stale.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            });
        db.Cases.Add(new Case
        {
            Id = _peerCaseId,
            TenantId = _tenantId,
            CustomerUserId = _peerCustomerId,
            Title = "Peer case",
            Status = CaseStatus.Submitted,
            FormData = """{"fullName":"Peer"}""",
            CreatedAt = now,
            UpdatedAt = now,
            SubmittedAt = now
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Demoted_reviewer_jwt_cannot_list_peer_cases()
    {
        Authenticate(UserRole.Reviewer, _tenantId, _reviewerId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == _reviewerId);
            user.Role = UserRole.Customer;
            await db.SaveChangesAsync();
        }

        var payload = await PostGraphqlAsync(
            """
            { "query": "{ cases { totalCount items { id } } }" }
            """);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("AUTH", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inactive_tenant_jwt_cannot_list_cases()
    {
        Authenticate(UserRole.Reviewer, _tenantId, _reviewerId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == _tenantId);
            tenant.IsActive = false;
            await db.SaveChangesAsync();
        }

        var payload = await PostGraphqlAsync(
            """
            { "query": "{ cases { totalCount items { id } } }" }
            """);

        Assert.True(payload.TryGetProperty("errors", out var errors), payload.ToString());
        Assert.Contains("AUTH", errors.ToString(), StringComparison.Ordinal);
    }

    private void Authenticate(UserRole role, Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = $"{role.ToString().ToLowerInvariant()}@stale.example",
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
