using FluentValidation;
using Kyc.Api.Application.Validation;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Application.Identity;

public sealed partial class LoginService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService jwtTokenService,
    ILogger<LoginService> logger,
    IValidator<LoginRequest> validator,
    ICaptchaVerifier captchaVerifier,
    ILoginLockoutStore lockoutStore)
{
    public const string GenericAuthFailure = "Invalid email, password, or tenant.";
    public const string RejectedLog = "Login rejected";
    public const int MaxPasswordLength = PasswordPolicy.MaxLength;

    private string? _dummyPasswordHash;

    private string DummyPasswordHash =>
        _dummyPasswordHash ??= passwordHasher.HashPassword(new User(), "kyc-login-dummy");

    public async Task<(LoginResponse? Result, IReadOnlyList<string> ValidationErrors, bool Unauthorized)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = RequestValidation.Errors(validator, request);
        if (validationErrors.Count > 0)
        {
            return (null, validationErrors, false);
        }

        var captcha = await captchaVerifier.VerifyAsync(
            request.CaptchaToken,
            CaptchaPurpose.Login,
            cancellationToken);
        if (!captcha.Passed)
        {
            return (null, [captcha.ErrorMessage ?? CaptchaMessages.Failed], false);
        }

        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        if (lockoutStore.IsLocked(slug, email, now))
        {
            RejectUnverified(request.Password);
            return (null, Array.Empty<string>(), true);
        }

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

        // Inactive tenant cannot log in (AC). Same generic message as bad credentials
        // so we do not leak whether the slug exists or the tenant is disabled.
        if (tenant?.IsActive is not true)
        {
            RejectUnverified(request.Password);
            lockoutStore.RecordFailure(slug, email, now);
            return (null, Array.Empty<string>(), true);
        }

        // Auth is cross-tenant by slug; ignore tenant filters if a JWT was also sent.
        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == email, cancellationToken);

        var hash = user?.PasswordHash ?? DummyPasswordHash;
        var verify = passwordHasher.VerifyHashedPassword(user ?? new User(), hash, request.Password);
        if (user is null || verify == PasswordVerificationResult.Failed)
        {
            LogRejected(logger);
            lockoutStore.RecordFailure(slug, email, now);
            return (null, Array.Empty<string>(), true);
        }

        lockoutStore.RecordSuccess(slug, email);
        var (token, expiresInSeconds) = jwtTokenService.CreateAccessToken(user);
        return (
            new LoginResponse(token, "Bearer", expiresInSeconds),
            Array.Empty<string>(),
            false);
    }

    private void RejectUnverified(string password)
    {
        passwordHasher.VerifyHashedPassword(new User(), DummyPasswordHash, password);
        LogRejected(logger);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = RejectedLog)]
    private static partial void LogRejected(ILogger logger);
}
