using System.IdentityModel.Tokens.Jwt;
using System.Text;
using HotChocolate.AspNetCore;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Kyc.Api.GraphQL;
using Kyc.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, HttpCurrentTenant>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnection))
{
    throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
}

var resilienceSection = builder.Configuration.GetSection(ResilienceOptions.SectionName);
builder.Services.Configure<ResilienceOptions>(resilienceSection);
var resilience = resilienceSection.Get<ResilienceOptions>() ?? new ResilienceOptions();
resilience.Validate();

builder.Services.AddSingleton(sp =>
    new PostgresReadyHealthCheck(
        postgresConnection,
        sp.GetRequiredService<ILogger<PostgresReadyHealthCheck>>()));

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<PostgresReadyHealthCheck>(
        "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConnection, npgsql =>
    {
        npgsql.CommandTimeout(resilience.NpgsqlCommandTimeoutSeconds);
        npgsql.EnableRetryOnFailure(
            maxRetryCount: resilience.EfMaxRetryCount,
            maxRetryDelay: TimeSpan.FromSeconds(resilience.EfMaxRetryDelaySeconds),
            errorCodesToAdd: null);
    }));

builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(resilience.RequestTimeoutSeconds)
    };
});

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be configured and at least 32 characters. " +
        "Copy appsettings.Development.json.example or set Jwt__SigningKey.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep ADR claim names (sub, role, tenant_id) as issued — needed for KYC-014.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var log = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Kyc.Api.Auth");
                log.LogWarning("JWT authentication failed {FailureType}", context.Exception.GetType().Name);
                return Task.CompletedTask;
            }
        };
    });

// Deny by default for ASP.NET endpoints (KYC-021). Opt in with AllowAnonymous.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<RegisterTenantService>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<CreateDraftCaseService>();
builder.Services.AddScoped<UpdateDraftCaseService>();

builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddErrorFilter<GraphQlAuthErrorLoggingFilter>()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .ModifyRequestOptions(options =>
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRequestTimeouts();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous().DisableRequestTimeout();

app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous().DisableRequestTimeout();

// HTTP endpoint is anonymous so login/register mutations can run;
// field auth (Query/Mutation [Authorize] + [AllowAnonymous]) enforces deny-by-default.
app.MapGraphQL("/graphql")
    .AllowAnonymous()
    .WithOptions(options =>
    {
        // Banana Cake Pop / Nitro IDE — Development only (KYC-020).
        options.Tool.Enable = app.Environment.IsDevelopment();
    });

// Temporary REST identity surface — same anonymous allow-list as GraphQL (KYC-021).
// Local Development uses HTTP — fine for Compose defaults only, not for real secrets.
app.MapPost("/api/register-tenant", async (
    RegisterTenantRequest request,
    RegisterTenantService service,
    CancellationToken cancellationToken) =>
{
    var (result, errors) = await service.RegisterAsync(request, cancellationToken);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    return Results.Json(result, statusCode: StatusCodes.Status201Created);
})
.WithName("RegisterTenant")
.AllowAnonymous()
.DisableAntiforgery();

app.MapPost("/api/login", async (
    LoginRequest request,
    LoginService service,
    CancellationToken cancellationToken) =>
{
    var (result, validationErrors, unauthorized) = await service.LoginAsync(request, cancellationToken);
    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(new { errors = validationErrors });
    }

    if (unauthorized || result is null)
    {
        return Results.Json(
            new { error = LoginService.GenericAuthFailure },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(result);
})
.WithName("Login")
.AllowAnonymous()
.DisableAntiforgery();

app.Run();

public partial class Program;
