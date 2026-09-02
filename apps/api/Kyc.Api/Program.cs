using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentValidation;
using Kyc.Api.Application.Audit;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Kyc.Api.GraphQL;
using Kyc.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: LiveHealthTags)
    .AddCheck<PostgresReadyHealthCheck>(
        "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ReadyHealthTags);

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
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(resilience.RequestTimeoutSeconds)
    });

// Allow a little headroom so application validation can return VALIDATION (not a raw 413/500).
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = DocumentUploadValidation.MaxFileBytes + (1024 * 1024));
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = DocumentUploadValidation.MaxFileBytes + (1024 * 1024));

var objectStorageSection = builder.Configuration.GetSection(ObjectStorageOptions.SectionName);
builder.Services.Configure<ObjectStorageOptions>(objectStorageSection);
var objectStorageOptions = objectStorageSection.Get<ObjectStorageOptions>() ?? new ObjectStorageOptions();
if (string.IsNullOrWhiteSpace(objectStorageOptions.Provider))
{
    throw new InvalidOperationException(
        "ObjectStorage:Provider is required. Use Minio for local/prod hosts or InMemory for tests. " +
        "Copy appsettings.Development.json.example or set ObjectStorage__Provider.");
}

if (string.Equals(objectStorageOptions.Provider, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    var allowInMemoryOutsideDev =
        builder.Configuration.GetValue("ObjectStorage:AllowInMemoryOutsideDevelopment", false);
    if (!builder.Environment.IsDevelopment() &&
        !builder.Environment.IsEnvironment("Testing") &&
        !allowInMemoryOutsideDev)
    {
        throw new InvalidOperationException(
            "ObjectStorage:Provider InMemory is only allowed in Development or Testing. Use Minio for other environments.");
    }

    builder.Services.AddSingleton<IObjectStorage, InMemoryObjectStorage>();
}
else if (string.Equals(objectStorageOptions.Provider, "Minio", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(objectStorageOptions.AccessKey) ||
        string.IsNullOrWhiteSpace(objectStorageOptions.SecretKey) ||
        string.IsNullOrWhiteSpace(objectStorageOptions.Endpoint) ||
        string.IsNullOrWhiteSpace(objectStorageOptions.BucketName))
    {
        throw new InvalidOperationException(
            "ObjectStorage MinIO settings require Endpoint, AccessKey, SecretKey, and BucketName. " +
            "Copy appsettings.Development.json.example or set ObjectStorage__*.");
    }

    builder.Services.AddSingleton<IObjectStorage, MinioObjectStorage>();
}
else
{
    throw new InvalidOperationException(
        $"Unknown ObjectStorage:Provider '{objectStorageOptions.Provider}'. Use Minio or InMemory.");
}

var corsSection = builder.Configuration.GetSection(CorsOptions.SectionName);
builder.Services.Configure<CorsOptions>(corsSection);
var corsOptions = corsSection.Get<CorsOptions>() ?? new CorsOptions();
var corsOrigins = corsOptions.AllowedOrigins
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.Ordinal)
    .ToArray();
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
        options.AddPolicy(CorsOptions.PolicyName, policy =>
            policy.WithOrigins(corsOrigins)
                .WithMethods("GET", "POST", "OPTIONS")
                .WithHeaders("Authorization", "Content-Type", "X-Request-Id", "Apollo-Require-Preflight")
                .WithExposedHeaders("X-Request-Id")));
}

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
            RoleClaimType = HttpCurrentUser.RoleClaimType
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var log = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Kyc.Api.Auth");
                Program.LogJwtAuthFailed(log, context.Exception.GetType().Name);
                return Task.CompletedTask;
            }
        };
    });

// Deny by default for ASP.NET endpoints (KYC-021). Opt in with AllowAnonymous.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.Configure<RegistrationOptions>(
    builder.Configuration.GetSection(RegistrationOptions.SectionName));
builder.Services.PostConfigure<RegistrationOptions>(options =>
    options.ApplyEnvironment(builder.Environment));
var registrationOptions = builder.Configuration
    .GetSection(RegistrationOptions.SectionName)
    .Get<RegistrationOptions>() ?? new RegistrationOptions();
if (registrationOptions.AllowPublicRegistration &&
    !builder.Environment.IsDevelopment() &&
    !registrationOptions.AllowInProduction)
{
    throw new InvalidOperationException(
        "Registration:AllowPublicRegistration is true outside Development. " +
        "Set Registration:AllowInProduction=true only as an explicit break-glass, or disable public registration.");
}

builder.Services.Configure<LoginLockoutOptions>(builder.Configuration.GetSection(LoginLockoutOptions.SectionName));
var lockoutOptions = builder.Configuration
    .GetSection(LoginLockoutOptions.SectionName)
    .Get<LoginLockoutOptions>() ?? new LoginLockoutOptions();
lockoutOptions.Validate();

builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection(CaptchaOptions.SectionName));
builder.Services.PostConfigure<CaptchaOptions>(options =>
    options.ApplyEnvironment(builder.Environment));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ILoginLockoutStore, MemoryLoginLockoutStore>();
builder.Services.AddHttpClient<ICaptchaVerifier, CaptchaVerifier>()
    .ConfigureHttpClient((sp, client) =>
    {
        var captcha = sp.GetRequiredService<IOptions<CaptchaOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, captcha.TimeoutSeconds));
    });

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddScoped<RegisterTenantService>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<DemoSeedService>();
builder.Services.AddScoped<CreateDraftCaseService>();
builder.Services.AddScoped<UpdateDraftCaseService>();
builder.Services.AddScoped<SubmitCaseService>();
builder.Services.AddScoped<StartCaseReviewService>();
builder.Services.AddScoped<CompleteCaseReviewService>();
builder.Services.AddScoped<ListCasesService>();
builder.Services.AddScoped<GetCaseDetailService>();
builder.Services.AddScoped<ListDocumentsService>();
builder.Services.AddScoped<UploadDocumentService>();
builder.Services.AddScoped<DownloadDocumentService>();
builder.Services.AddScoped<ListCaseAuditService>();

builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddErrorFilter<GraphQlAuthErrorLoggingFilter>()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    // Explicit: HC default security already turns introspection off outside Development.
    .DisableIntrospection(!builder.Environment.IsDevelopment())
    .AddMaxExecutionDepthRule(maxAllowedExecutionDepth: 10, skipIntrospectionFields: true)
    .ModifyRequestOptions(options =>
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment());

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Development: trust local reverse proxies so rate-limit partitions use the real client IP.
    // Production: keep KnownProxies/Networks defaults (or set them explicitly) — do not clear blindly.
    if (builder.Environment.IsDevelopment())
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

var authLimits = AuthLimitsOptions.Bind(builder.Configuration, builder.Environment);
authLimits.Validate();
builder.Services.AddRateLimiter(options => AuthRateLimiting.Configure(options, authLimits));

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}
else
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
if (corsOrigins.Length > 0)
{
    app.UseCors(CorsOptions.PolicyName);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<GraphQlOperationClassifierMiddleware>();
app.UseRateLimiter();
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
    .RequireRateLimiting(AuthRateLimiting.GraphqlPolicy)
    .WithOptions(options =>
    {
        var development = app.Environment.IsDevelopment();
        // Banana Cake Pop / Nitro IDE — Development only (KYC-020).
        options.Tool.Enable = development;
        // SDL via ?sdl / schema file — same gate (KYC-105).
        options.EnableSchemaRequests = development;
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
.RequireRateLimiting(AuthRateLimiting.RegisterPolicy)
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
.RequireRateLimiting(AuthRateLimiting.LoginPolicy)
.DisableAntiforgery();

// Document upload (KYC-040) — multipart bytes on REST; metadata via GraphQL case detail.
app.MapPost("/api/cases/{caseId:guid}/documents", async (
    Guid caseId,
    HttpRequest request,
    UploadDocumentService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { errors = MultipartFormRequiredErrors, code = "VALIDATION" });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");

    var (result, validationErrors, unauthorized, forbidden, errorCode, errorMessage) =
        await service.UploadAsync(caseId, file, cancellationToken);

    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(new { errors = validationErrors, code = "VALIDATION" });
    }

    if (unauthorized)
    {
        return Results.Json(
            new { error = CreateDraftCaseService.GenericAuthFailure, code = "AUTH_FAILED" },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    if (forbidden)
    {
        return Results.Json(
            new { error = UploadDocumentService.ForbiddenMessage, code = "AUTH_NOT_AUTHORIZED" },
            statusCode: StatusCodes.Status403Forbidden);
    }

    if (errorCode == "NOT_FOUND")
    {
        return Results.Json(
            new { error = errorMessage ?? UploadDocumentService.NotFoundMessage, code = "NOT_FOUND" },
            statusCode: StatusCodes.Status404NotFound);
    }

    if (errorCode == "STORAGE")
    {
        return Results.Json(
            new { error = errorMessage ?? "Could not store the document. Please try again.", code = "STORAGE" },
            statusCode: StatusCodes.Status502BadGateway);
    }

    if (errorCode is not null)
    {
        return Results.Json(
            new { error = errorMessage ?? "Request failed.", code = errorCode },
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    return Results.Json(result, statusCode: StatusCodes.Status201Created);
})
.WithName("UploadDocument")
.RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Customer })
.DisableAntiforgery()
.WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(
    DocumentUploadValidation.MaxFileBytes + (1024 * 1024)));

// Document download (KYC-042) — authenticated stream; same case visibility as GraphQL documents list.
app.MapGet("/api/cases/{caseId:guid}/documents/{documentId:guid}", async (
    Guid caseId,
    Guid documentId,
    DownloadDocumentService service,
    CancellationToken cancellationToken) =>
{
    var (result, validationErrors, unauthorized, errorCode, errorMessage) =
        await service.DownloadAsync(caseId, documentId, cancellationToken);

    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(new { errors = validationErrors, code = "VALIDATION" });
    }

    if (unauthorized)
    {
        return Results.Json(
            new { error = CreateDraftCaseService.GenericAuthFailure, code = "AUTH_FAILED" },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    if (errorCode == "NOT_FOUND")
    {
        return Results.Json(
            new { error = errorMessage ?? DownloadDocumentService.NotFoundMessage, code = "NOT_FOUND" },
            statusCode: StatusCodes.Status404NotFound);
    }

    if (errorCode == "STORAGE")
    {
        return Results.Json(
            new { error = errorMessage ?? "Could not read the document. Please try again.", code = "STORAGE" },
            statusCode: StatusCodes.Status502BadGateway);
    }

    if (errorCode is not null)
    {
        return Results.Json(
            new { error = errorMessage ?? "Request failed.", code = errorCode },
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    // Results.File disposes the stream after the response completes.
    return Results.File(
        result!.Content,
        result.ContentType,
        fileDownloadName: result.FileName,
        enableRangeProcessing: false);
})
.WithName("DownloadDocument")
.RequireAuthorization(new AuthorizeAttribute
{
    Roles = $"{AuthRoles.Customer},{AuthRoles.Reviewer},{AuthRoles.TenantAdmin}"
});

await SeedDemoDataIfEnabledAsync(app);
app.Run();

public partial class Program
{
    private static readonly string[] LiveHealthTags = ["live"];
    private static readonly string[] ReadyHealthTags = ["ready"];
    private static readonly string[] MultipartFormRequiredErrors =
        ["multipart/form-data with a file field is required."];

    [LoggerMessage(Level = LogLevel.Warning, Message = "JWT authentication failed {FailureType}")]
    internal static partial void LogJwtAuthFailed(ILogger logger, string failureType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo seed blob repair failed ({ExceptionType}).")]
    private static partial void LogSeedBackgroundFailed(ILogger logger, string exceptionType);

    private static async Task SeedDemoDataIfEnabledAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        var enabled = app.Configuration
            .GetSection(SeedOptions.SectionName)
            .Get<SeedOptions>()?.Enabled ?? true;
        if (!enabled)
        {
            return;
        }

        bool prepared;
        using (var scope = app.Services.CreateScope())
        {
            prepared = await scope.ServiceProvider
                .GetRequiredService<DemoSeedService>()
                .SeedRowsAsync(app.Lifetime.ApplicationStopping);
        }

        if (!prepared)
        {
            return;
        }

        app.Lifetime.ApplicationStarted.Register(() => _ = RepairSeedBlobsAfterStartAsync(app));
    }

    private static async Task RepairSeedBlobsAfterStartAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider
                .GetRequiredService<DemoSeedService>()
                .RepairSeedBlobsAsync(app.Lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            LogSeedBackgroundFailed(app.Logger, ex.GetType().Name);
        }
    }
}
