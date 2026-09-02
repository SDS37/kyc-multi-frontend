using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Validation;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

public sealed class FluentValidationPipelineTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Validation Co",
            Slug = $"val-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@validation.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = now
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Request_validators_are_registered()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetService<IValidator<RegisterTenantRequest>>());
        Assert.NotNull(services.GetService<IValidator<LoginRequest>>());
        Assert.NotNull(services.GetService<IValidator<CreateDraftCaseRequest>>());
        Assert.NotNull(services.GetService<IValidator<UpdateDraftCaseRequest>>());
        Assert.NotNull(services.GetService<IValidator<SubmitCaseRequest>>());
        Assert.NotNull(services.GetService<IValidator<StartCaseReviewRequest>>());
        Assert.NotNull(services.GetService<IValidator<ApproveCaseRequest>>());
        Assert.NotNull(services.GetService<IValidator<RejectCaseRequest>>());
        Assert.NotNull(services.GetService<IValidator<ListCasesRequest>>());
        Assert.NotNull(services.GetService<IValidator<CaseIdInput>>());
        Assert.NotNull(services.GetService<IValidator<DownloadDocumentIds>>());
    }

    [Fact]
    public async Task Empty_title_is_VALIDATION_not_HTTP_500()
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "customer@validation.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                """{"query":"mutation($input: CreateDraftCaseRequestInput!) { createDraftCase(input: $input) { id } }","variables":{"input":{"title":""}}}""",
                Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains("Title is required", errors, StringComparison.Ordinal);
    }
}
