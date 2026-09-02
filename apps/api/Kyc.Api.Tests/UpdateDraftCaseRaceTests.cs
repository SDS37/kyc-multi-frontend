using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kyc.Api.Tests;

/// <summary>
/// Flips the case to Submitted during payload validation so persist-time
/// <c>ExecuteUpdate</c> where Draft is proven (KYC-095). Concurrent HTTP is not used:
/// two in-flight requests share one SQLite connection and the winner is not deterministic.
/// </summary>
public sealed class UpdateDraftRaceState
{
    public Guid CaseId { get; set; }
}

public sealed class FlipToSubmittedOnPayloadValidator(
    IValidator<UpdateDraftCaseRequest> inner,
    IServiceScopeFactory scopes,
    UpdateDraftRaceState state) : IValidator<UpdateDraftCaseRequest>
{
    public ValidationResult Validate(UpdateDraftCaseRequest instance) => inner.Validate(instance);

    public Task<ValidationResult> ValidateAsync(
        UpdateDraftCaseRequest instance,
        CancellationToken cancellation = default) =>
        inner.ValidateAsync(instance, cancellation);

    public ValidationResult Validate(IValidationContext context)
    {
        FlipToSubmitted();
        return ((IValidator)inner).Validate(context);
    }

    public Task<ValidationResult> ValidateAsync(
        IValidationContext context,
        CancellationToken cancellation = default)
    {
        FlipToSubmitted();
        return ((IValidator)inner).ValidateAsync(context, cancellation);
    }

    public IValidatorDescriptor CreateDescriptor() => inner.CreateDescriptor();

    public bool CanValidateInstancesOfType(Type type) => inner.CanValidateInstancesOfType(type);

    private void FlipToSubmitted()
    {
        if (state.CaseId == Guid.Empty)
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.Cases
            .IgnoreQueryFilters()
            .Where(c => c.Id == state.CaseId && c.Status == CaseStatus.Draft)
            .ExecuteUpdate(setters => setters
                .SetProperty(c => c.Status, CaseStatus.Submitted)
                .SetProperty(c => c.SubmittedAt, now)
                .SetProperty(c => c.UpdatedAt, now));
    }
}

public sealed class UpdateDraftRaceFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<UpdateDraftRaceState>();
            services.RemoveAll<IValidator<UpdateDraftCaseRequest>>();
            services.AddScoped<IValidator<UpdateDraftCaseRequest>>(sp =>
                new FlipToSubmittedOnPayloadValidator(
                    new UpdateDraftCaseRequestValidator(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<UpdateDraftRaceState>()));
        });
    }
}

public sealed class UpdateDraftCaseRaceTests(UpdateDraftRaceFactory factory)
    : IClassFixture<UpdateDraftRaceFactory>, IAsyncLifetime
{
    private const string OriginalTitle = "Race draft";

    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;
    private Guid _draftCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _draftCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Draft Race Co",
            Slug = $"dfrace-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "c@dfrace.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = now
        });
        db.Cases.Add(new Case
        {
            Id = _draftCaseId,
            TenantId = _tenantId,
            CustomerUserId = _customerId,
            Title = OriginalTitle,
            Status = CaseStatus.Draft,
            FormData = """{"fullName":"Ada Lovelace"}""",
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<UpdateDraftRaceState>().CaseId = _draftCaseId;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Persist_recheck_rejects_update_after_status_leaves_draft()
    {
        using var jwtScope = factory.Services.CreateScope();
        var jwt = jwtScope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "c@dfrace.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id title status } }",
                  "variables": { "input": { "id": "{{_draftCaseId}}", "title": "Should not stick if submitted" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.Contains(UpdateDraftCaseService.NotDraftMessage, errors, StringComparison.Ordinal);

        using var verify = factory.Services.CreateScope();
        var stored = await verify.ServiceProvider.GetRequiredService<AppDbContext>()
            .Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == _draftCaseId);
        Assert.Equal(CaseStatus.Submitted, stored.Status);
        Assert.Equal(OriginalTitle, stored.Title);
    }
}

public sealed class FlipFormDataOnPayloadValidator(
    IValidator<UpdateDraftCaseRequest> inner,
    IServiceScopeFactory scopes,
    UpdateDraftFormDataRaceState state) : IValidator<UpdateDraftCaseRequest>
{
    public ValidationResult Validate(UpdateDraftCaseRequest instance) => inner.Validate(instance);

    public Task<ValidationResult> ValidateAsync(
        UpdateDraftCaseRequest instance,
        CancellationToken cancellation = default) =>
        inner.ValidateAsync(instance, cancellation);

    public ValidationResult Validate(IValidationContext context)
    {
        FlipFormData();
        return ((IValidator)inner).Validate(context);
    }

    public Task<ValidationResult> ValidateAsync(
        IValidationContext context,
        CancellationToken cancellation = default)
    {
        FlipFormData();
        return ((IValidator)inner).ValidateAsync(context, cancellation);
    }

    public IValidatorDescriptor CreateDescriptor() => inner.CreateDescriptor();

    public bool CanValidateInstancesOfType(Type type) => inner.CanValidateInstancesOfType(type);

    private void FlipFormData()
    {
        if (state.CaseId == Guid.Empty)
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Cases
            .IgnoreQueryFilters()
            .Where(c => c.Id == state.CaseId && c.Status == CaseStatus.Draft)
            .ExecuteUpdate(setters => setters.SetProperty(c => c.FormData, state.NewerFormData));
    }
}

public sealed class UpdateDraftFormDataRaceState
{
    public Guid CaseId { get; set; }

    public string NewerFormData { get; set; } = """{"keep":false,"v":2}""";
}

public sealed class UpdateDraftFormDataRaceFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<UpdateDraftFormDataRaceState>();
            services.RemoveAll<IValidator<UpdateDraftCaseRequest>>();
            services.AddScoped<IValidator<UpdateDraftCaseRequest>>(sp =>
                new FlipFormDataOnPayloadValidator(
                    new UpdateDraftCaseRequestValidator(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<UpdateDraftFormDataRaceState>()));
        });
    }
}

public sealed class UpdateDraftFormDataOmitTests(UpdateDraftFormDataRaceFactory factory)
    : IClassFixture<UpdateDraftFormDataRaceFactory>, IAsyncLifetime
{
    private const string OriginalFormData = """{"keep":true}""";
    private const string NewerFormData = """{"keep":false,"v":2}""";

    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;
    private Guid _draftCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _draftCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Form Race Co",
            Slug = $"ffrace-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "c@ffrace.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = now
        });
        db.Cases.Add(new Case
        {
            Id = _draftCaseId,
            TenantId = _tenantId,
            CustomerUserId = _customerId,
            Title = "Original title",
            Status = CaseStatus.Draft,
            FormData = OriginalFormData,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        var state = scope.ServiceProvider.GetRequiredService<UpdateDraftFormDataRaceState>();
        state.CaseId = _draftCaseId;
        state.NewerFormData = NewerFormData;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Title_only_update_does_not_clobber_formData_changed_after_read()
    {
        using var jwtScope = factory.Services.CreateScope();
        var jwt = jwtScope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "c@ffrace.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: UpdateDraftCaseRequestInput!) { updateDraftCase(input: $input) { id title formData } }",
                  "variables": { "input": { "id": "{{_draftCaseId}}", "title": "Title only" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.TryGetProperty("errors", out _), document.RootElement.ToString());

        using var verify = factory.Services.CreateScope();
        var stored = await verify.ServiceProvider.GetRequiredService<AppDbContext>()
            .Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == _draftCaseId);
        Assert.Equal("Title only", stored.Title);
        Assert.Equal(NewerFormData, stored.FormData);
    }
}
