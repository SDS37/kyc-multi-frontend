using System.Net.Mail;
using System.Text.RegularExpressions;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kyc.Api.Application.Identity;

public sealed partial class RegisterTenantService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<RegistrationOptions> registrationOptions)
{
    private const int MaxPasswordLength = PasswordPolicy.MaxLength;
    public const string RegistrationDisabledMessage = "Public tenant registration is disabled.";

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public async Task<(RegisterTenantResponse? Result, IReadOnlyList<string> Errors)> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!registrationOptions.Value.AllowPublicRegistration)
        {
            return (null, [RegistrationDisabledMessage]);
        }

        var errors = Validate(request);
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

    private static List<string> Validate(RegisterTenantRequest request)
    {
        var errors = new List<string>();

        var name = request.TenantName?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 200)
        {
            errors.Add("Tenant name must be between 2 and 200 characters.");
        }

        var slug = request.TenantSlug?.Trim().ToLowerInvariant() ?? string.Empty;
        if (slug.Length is < 2 or > 100)
        {
            errors.Add("Tenant slug must be between 2 and 100 characters.");
        }
        else if (!SlugPattern().IsMatch(slug))
        {
            errors.Add("Tenant slug must be lowercase letters, numbers, and hyphens (no leading/trailing hyphen).");
        }

        var email = request.AdminEmail?.Trim() ?? string.Empty;
        if (email.Length is 0 or > 320 || !IsValidEmail(email))
        {
            errors.Add("A valid admin email is required.");
        }

        var password = request.AdminPassword ?? string.Empty;
        if (password.Length < 8)
        {
            errors.Add("Password must be at least 8 characters.");
        }
        else if (password.Length > MaxPasswordLength)
        {
            errors.Add($"Password must be at most {MaxPasswordLength} characters.");
        }
        else if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one letter and one digit.");
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
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
