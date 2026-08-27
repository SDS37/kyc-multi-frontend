using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnection))
{
    throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConnection));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<RegisterTenantService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Public registration endpoint (no JWT). Temporary REST until Hot Chocolate (KYC-020).
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

    return Results.Created($"/api/tenants/{result!.TenantId}", result);
})
.WithName("RegisterTenant")
.AllowAnonymous();

app.Run();
