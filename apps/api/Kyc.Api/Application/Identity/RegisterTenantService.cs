using System.Net.Mail;
using System.Text.RegularExpressions;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Identity;

public sealed partial class RegisterTenantService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher)
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public async Task<(RegisterTenantResponse? Result, IReadOnlyList<string> Errors)> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken cancellationToken = default)
    {
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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Tenants.Add(tenant);
            db.Users.Add(admin);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (null, ["Could not register tenant. The slug or email may already be in use."]);
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
            _ = new MailAddress(email);
            return email.Contains('@');
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
