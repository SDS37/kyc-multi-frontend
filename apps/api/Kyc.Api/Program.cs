using System.IdentityModel.Tokens.Jwt;
using System.Text;
using HotChocolate.AspNetCore;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Kyc.Api.GraphQL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, HttpCurrentTenant>();
builder.Services.AddHealthChecks();

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnection))
{
    throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConnection));

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
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<RegisterTenantService>();
builder.Services.AddScoped<LoginService>();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapGraphQL("/graphql")
    .WithOptions(options =>
    {
        // Banana Cake Pop / Nitro IDE — Development only (KYC-020).
        options.Tool.Enable = app.Environment.IsDevelopment();
    });

// Temporary public REST until GraphQL mutations land (KYC-021).
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
.DisableAntiforgery();

app.Run();
