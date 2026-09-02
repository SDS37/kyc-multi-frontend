using FluentValidation;
using Kyc.Api.Application.Validation;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kyc.Api.Application.Identity;

public sealed class RegisterTenantService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<RegistrationOptions> registrationOptions,
    IValidator<RegisterTenantRequest> validator)
{
    public const string RegistrationDisabledMessage = "Public tenant registration is disabled.";

    public async Task<(RegisterTenantResponse? Result, IReadOnlyList<string> Errors)> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!registrationOptions.Value.AllowPublicRegistration)
        {
            return (null, [RegistrationDisabledMessage]);
        }

        var errors = RequestValidation.Errors(validator, request);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var name = request.TenantName.Trim();
        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        var email = request.AdminEmail.Trim().ToLowerInvariant();

        var slugTaken = await db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);
        if (slugTaken)
        {
            return (null, ["Tenant slug is already taken."]);
        }

        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsActive = true,
            CreatedAt = now
        };

        var admin = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = email,
            Role = UserRole.TenantAdmin,
            CreatedAt = now
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, request.AdminPassword);

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                db.Tenants.Add(tenant);
                db.Users.Add(admin);
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Unique slug/email race. Do not claim a specific cause.
            return (null, ["Could not register tenant. Please try a different slug."]);
        }

        return (
            new RegisterTenantResponse(tenant.Id, tenant.Slug, admin.Id, admin.Email),
            Array.Empty<string>());
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }

            // SQLite test host (no Npgsql exception type).
            if (inner.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
