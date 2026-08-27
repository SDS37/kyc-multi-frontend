using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Identity;

public sealed class LoginService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService jwtTokenService)
{
    public const string GenericAuthFailure = "Invalid email, password, or tenant.";

    public async Task<(LoginResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return (null, validationErrors, false);
        }

        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

        // Inactive tenant cannot log in (AC). Same generic message as bad credentials
        // so we do not leak whether the slug exists or the tenant is disabled.
        if (tenant is null || !tenant.IsActive)
        {
            return (null, Array.Empty<string>(), true);
        }

        // Auth is cross-tenant by slug; ignore tenant filters if a JWT was also sent.
        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == email, cancellationToken);

        if (user is null)
        {
            return (null, Array.Empty<string>(), true);
        }

        var verify = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return (null, Array.Empty<string>(), true);
        }

        var (token, expiresInSeconds) = jwtTokenService.CreateAccessToken(user);
        return (
            new LoginResponse(token, "Bearer", expiresInSeconds),
            Array.Empty<string>(),
            false);
    }

    private static List<string> Validate(LoginRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            errors.Add("Tenant slug is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Password is required.");
        }

        return errors;
    }
}
