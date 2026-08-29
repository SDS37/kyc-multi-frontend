using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class ListCasesTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _reviewerId;
    private Guid _adminId;
    private Guid _customerAId;
    private Guid _customerBId;
    private Guid _otherTenantCustomerId;
    private Guid _customerADraftId;
    private Guid _customerASubmittedId;
    private Guid _customerBDraftId;
    private Guid _otherTenantCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();

        _tenantId = Guid.NewGuid();
        _otherTenantId = Guid.NewGuid();
        _reviewerId = Guid.NewGuid();
        _adminId = Guid.NewGuid();
        _customerAId = Guid.NewGuid();
        _customerBId = Guid.NewGuid();
        _otherTenantCustomerId = Guid.NewGuid();
        _customerADraftId = Guid.NewGuid();
        _customerASubmittedId = Guid.NewGuid();
        _customerBDraftId = Guid.NewGuid();
        _otherTenantCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = _tenantId,
                Name = "List Co",
                Slug = $"lst-{_tenantId:N}"[..20],
                IsActive = true,
                CreatedAt = now
            },
            new Tenant
            {
                Id = _otherTenantId,
                Name = "Other Co",
                Slug = $"oth-{_otherTenantId:N}"[..20],
                IsActive = true,
                CreatedAt = now
            });
        db.Users.AddRange(
            new User
            {
                Id = _reviewerId,
                TenantId = _tenantId,
                Email = "reviewer@list.example",
                PasswordHash = "unused",
                Role = UserRole.Reviewer,
                CreatedAt = now
            },
            new User
            {
                Id = _adminId,
                TenantId = _tenantId,
                Email = "admin@list.example",
                PasswordHash = "unused",
                Role = UserRole.TenantAdmin,
                CreatedAt = now
            },
            new User
            {
                Id = _customerAId,
                TenantId = _tenantId,
                Email = "customer-a@list.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _customerBId,
                TenantId = _tenantId,
                Email = "customer-b@list.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            },
            new User
            {
                Id = _otherTenantCustomerId,
                TenantId = _otherTenantId,
                Email = "customer@oth.example",
                PasswordHash = "unused",
                Role = UserRole.Customer,
                CreatedAt = now
            });
        db.Cases.AddRange(
            new Case
            {
                Id = _customerADraftId,
                TenantId = _tenantId,
                CustomerUserId = _customerAId,
                Title = "A draft",
                Status = CaseStatus.Draft,
                FormData = "{}",
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            },
            new Case
            {
                Id = _customerASubmittedId,
                TenantId = _tenantId,
                CustomerUserId = _customerAId,
                Title = "A submitted",
                Status = CaseStatus.Submitted,
                FormData = """{"fullName":"Ada"}""",
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-1),
                SubmittedAt = now.AddMinutes(-1)
            },
            new Case
            {
                Id = _customerBDraftId,
                TenantId = _tenantId,
                CustomerUserId = _customerBId,
                Title = "B draft",
                Status = CaseStatus.Draft,
                FormData = "{}",
                CreatedAt = now.AddMinutes(-4),
                UpdatedAt = now.AddMinutes(-4)
            },
            new Case
            {
                Id = _otherTenantCaseId,
                TenantId = _otherTenantId,
                CustomerUserId = _otherTenantCustomerId,
                Title = "Other tenant",
                Status = CaseStatus.Submitted,
                FormData = "{}",
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
    public async Task Customer_sees_only_own_cases()
    {
        Authenticate(UserRole.Customer, _customerAId);

        var payload = await PostGraphqlAsync(
            """
            {
              "query": "query { cases { totalCount items { id title customerUserId } } }"
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var list = payload.GetProperty("data").GetProperty("cases");
        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
        var ids = list.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToHashSet();
        Assert.Contains(_customerADraftId.ToString(), ids);
        Assert.Contains(_customerASubmittedId.ToString(), ids);
        Assert.DoesNotContain(_customerBDraftId.ToString(), ids);
        Assert.DoesNotContain(_otherTenantCaseId.ToString(), ids);
    }

    [Fact]
    public async Task Reviewer_sees_all_tenant_cases()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        var payload = await PostGraphqlAsync(
            """
            {
              "query": "query { cases { totalCount items { id } } }"
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var list = payload.GetProperty("data").GetProperty("cases");
        Assert.Equal(3, list.GetProperty("totalCount").GetInt32());
        var ids = list.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToHashSet();
        Assert.Contains(_customerADraftId.ToString(), ids);
        Assert.Contains(_customerASubmittedId.ToString(), ids);
        Assert.Contains(_customerBDraftId.ToString(), ids);
        Assert.DoesNotContain(_otherTenantCaseId.ToString(), ids);
    }

    [Fact]
    public async Task TenantAdmin_sees_all_tenant_cases()
    {
        Authenticate(UserRole.TenantAdmin, _adminId);

        var payload = await PostGraphqlAsync(
            """
            {
              "query": "query { cases { totalCount items { id } } }"
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        Assert.Equal(3, payload.GetProperty("data").GetProperty("cases").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Filter_by_status()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        var payload = await PostGraphqlAsync(
            """
            {
              "query": "query { cases(status: SUBMITTED) { totalCount items { id status } } }"
            }
            """);

        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        var list = payload.GetProperty("data").GetProperty("cases");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        var item = list.GetProperty("items")[0];
        Assert.Equal(_customerASubmittedId.ToString(), item.GetProperty("id").GetString());
        Assert.Equal("SUBMITTED", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Pagination_skip_and_take()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        var page1 = await PostGraphqlAsync(
            """
            {
              "query": "query { cases(skip: 0, take: 2) { totalCount skip take items { id } } }"
            }
            """);

        Assert.False(page1.TryGetProperty("errors", out _), page1.ToString());
        var list1 = page1.GetProperty("data").GetProperty("cases");
        Assert.Equal(3, list1.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, list1.GetProperty("skip").GetInt32());
        Assert.Equal(2, list1.GetProperty("take").GetInt32());
        Assert.Equal(2, list1.GetProperty("items").GetArrayLength());

        var page2 = await PostGraphqlAsync(
            """
            {
              "query": "query { cases(skip: 2, take: 2) { totalCount skip take items { id } } }"
            }
            """);

        Assert.False(page2.TryGetProperty("errors", out _), page2.ToString());
        var list2 = page2.GetProperty("data").GetProperty("cases");
        Assert.Equal(3, list2.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, list2.GetProperty("items").GetArrayLength());

        var allIds = list1.GetProperty("items").EnumerateArray()
            .Concat(list2.GetProperty("items").EnumerateArray())
            .Select(e => e.GetProperty("id").GetString())
            .ToHashSet();
        Assert.Equal(3, allIds.Count);
    }

    [Fact]
    public async Task Invalid_take_returns_VALIDATION()
    {
        Authenticate(UserRole.Reviewer, _reviewerId);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "query { cases(take: {{ListCasesService.MaxPageSize + 1}}) { totalCount } }"
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("VALIDATION", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                """
                {
                  "query": "query { cases { totalCount } }"
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("AUTH_NOT_AUTHENTICATED", document.RootElement.GetProperty("errors").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_JWT_returns_AUTH_FAILED()
    {
        Authenticate(UserRole.Customer, Guid.NewGuid());

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                """
                {
                  "query": "query { cases { totalCount } }"
                }
                """,
                Encoding.UTF8,
                "application/json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("AUTH_FAILED", errors, StringComparison.Ordinal);
        Assert.Contains(CreateDraftCaseService.GenericAuthFailure, errors, StringComparison.Ordinal);
    }

    private void Authenticate(UserRole role, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = userId,
            TenantId = _tenantId,
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
