using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

/// <summary>
/// KYC-092: GraphQL critical path and tenant isolation. Register only creates TenantAdmin;
/// a Customer is inserted with the same password hasher so create/submit still go through login.
/// </summary>
public sealed class HappyPathAndIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string Password = "ChangeMe1234";
    private const string SubmitFormData = """
        {"fullName":"Ada Lovelace","dateOfBirth":"1815-12-10","nationality":"British","address":"12 Analytical Engine Rd"}
        """;

    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Register_login_create_submit_review_approve()
    {
        var slug = UniqueSlug("hp");
        var adminEmail = $"admin@{slug}.example";
        var customerEmail = $"customer@{slug}.example";

        var registered = await RegisterTenantAsync(slug, adminEmail);
        var tenantId = registered.GetProperty("tenantId").GetGuid();

        await LoginAsync(slug, adminEmail);
        await AddCustomerAsync(tenantId, customerEmail);

        await LoginAsync(slug, customerEmail);
        var created = Data(await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id status } }",
              "variables": {
                "input": {
                  "title": "Onboarding Ada",
                  "formData": {{JsonSerializer.Serialize(SubmitFormData)}}
                }
              }
            }
            """)).GetProperty("createDraftCase");
        var caseId = created.GetProperty("id").GetGuid();
        Assert.Equal("DRAFT", created.GetProperty("status").GetString());

        var submitted = Data(await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}" } }
            }
            """)).GetProperty("submitCase");
        Assert.Equal("SUBMITTED", submitted.GetProperty("status").GetString());

        await LoginAsync(slug, adminEmail);

        var inReview = Data(await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: StartCaseReviewRequestInput!) { startCaseReview(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}" } }
            }
            """)).GetProperty("startCaseReview");
        Assert.Equal("IN_REVIEW", inReview.GetProperty("status").GetString());

        var approved = Data(await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: ApproveCaseRequestInput!) { approveCase(input: $input) { id status } }",
              "variables": { "input": { "id": "{{caseId}}" } }
            }
            """)).GetProperty("approveCase");
        Assert.Equal("APPROVED", approved.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Tenant_B_cannot_read_tenant_A_case()
    {
        var slugA = UniqueSlug("isa");
        var slugB = UniqueSlug("isb");
        var adminA = $"admin@{slugA}.example";
        var adminB = $"admin@{slugB}.example";
        var customerA = $"customer@{slugA}.example";
        var customerB = $"customer@{slugB}.example";

        var tenantA = (await RegisterTenantAsync(slugA, adminA)).GetProperty("tenantId").GetGuid();
        var tenantB = (await RegisterTenantAsync(slugB, adminB)).GetProperty("tenantId").GetGuid();
        await AddCustomerAsync(tenantA, customerA);
        await AddCustomerAsync(tenantB, customerB);

        await LoginAsync(slugA, customerA);
        var caseId = Data(await PostGraphqlAsync("""
            {
              "query": "mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id } }",
              "variables": { "input": { "title": "Tenant A secret" } }
            }
            """)).GetProperty("createDraftCase").GetProperty("id").GetGuid();

        await LoginAsync(slugB, customerB);

        var detail = await PostGraphqlAsync($$"""
            {
              "query": "query($id: UUID!) { case(id: $id) { case { id } } }",
              "variables": { "id": "{{caseId}}" }
            }
            """);
        Assert.True(detail.TryGetProperty("errors", out var detailErrors), detail.ToString());
        Assert.Contains("NOT_FOUND", detailErrors.ToString(), StringComparison.Ordinal);

        var list = Data(await PostGraphqlAsync("""
            { "query": "query { cases { totalCount items { id } } }" }
            """)).GetProperty("cases");
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, list.GetProperty("items").GetArrayLength());

        await LoginAsync(slugB, adminB);
        var adminList = Data(await PostGraphqlAsync("""
            { "query": "query { cases { totalCount items { id } } }" }
            """)).GetProperty("cases");
        Assert.Equal(0, adminList.GetProperty("totalCount").GetInt32());
    }

    private async Task<JsonElement> RegisterTenantAsync(string slug, string adminEmail)
    {
        var payload = await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantId tenantSlug } }",
              "variables": {
                "input": {
                  "tenantName": "{{slug}} Co",
                  "tenantSlug": "{{slug}}",
                  "adminEmail": "{{adminEmail}}",
                  "adminPassword": "{{Password}}"
                }
              }
            }
            """);
        return Data(payload).GetProperty("registerTenant");
    }

    private async Task LoginAsync(string slug, string email)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var payload = await PostGraphqlAsync($$"""
            {
              "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken } }",
              "variables": {
                "input": {
                  "tenantSlug": "{{slug}}",
                  "email": "{{email}}",
                  "password": "{{Password}}"
                }
              }
            }
            """);
        var token = Data(payload).GetProperty("login").GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task AddCustomerAsync(Guid tenantId, string email)
    {
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        customer.PasswordHash = hasher.HashPassword(customer, Password);
        db.Users.Add(customer);
        await db.SaveChangesAsync();
    }

    private static JsonElement Data(JsonElement payload)
    {
        Assert.False(payload.TryGetProperty("errors", out _), payload.ToString());
        return payload.GetProperty("data");
    }

    private static string UniqueSlug(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16];

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
