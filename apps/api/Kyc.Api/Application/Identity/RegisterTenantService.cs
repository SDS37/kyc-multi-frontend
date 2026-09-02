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
    ICaptchaVerifier captchaVerifier,
    IValidator<RegisterTenantRequest> validator)
{
    public const string RegistrationDisabledMessage = "Public tenant registration is disabled.";
    public const string InviteRequiredMessage = "Invite code is required.";
    public const string GenericRegisterFailure = "Could not register tenant.";

    public async Task<(RegisterTenantResponse? Result, IReadOnlyList<string> Errors)> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = RequestValidation.Errors(validator, request);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var captcha = await captchaVerifier.VerifyAsync(
            request.CaptchaToken,
            CaptchaPurpose.Register,
            cancellationToken);
        if (!captcha.Passed)
        {
            return (null, [captcha.ErrorMessage ?? CaptchaMessages.Failed]);
        }

        var settings = registrationOptions.Value;
        var inviteCode = request.InviteCode?.Trim();
        var hasInvite = !string.IsNullOrWhiteSpace(inviteCode);

        if (!settings.AllowPublicRegistration && !hasInvite)
        {
            return (null, [RegistrationDisabledMessage]);
        }

        if (settings.AllowPublicRegistration && settings.InviteRequired && !hasInvite)
        {
            return (null, [InviteRequiredMessage]);
        }

        Guid? inviteId = null;
        if (hasInvite)
        {
            var invite = await FindRedeemableInviteAsync(inviteCode!, cancellationToken);
            if (invite is null)
            {
                return (
                    null,
                    [settings.AllowPublicRegistration ? GenericRegisterFailure : RegistrationDisabledMessage]);
            }

            inviteId = invite.Id;
        }

        var name = request.TenantName.Trim();
        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        var email = request.AdminEmail.Trim().ToLowerInvariant();

        var slugTaken = await db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);
        if (slugTaken)
        {
            return (null, [GenericRegisterFailure]);
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
        var inviteLost = false;
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                inviteLost = false;
                db.Tenants.Add(tenant);
                db.Users.Add(admin);
                if (inviteId is { } id)
                {
                    var row = await db.RegistrationInvites.FirstOrDefaultAsync(
                        i => i.Id == id && i.RedeemedAt == null,
                        cancellationToken);
                    if (row is null)
                    {
                        inviteLost = true;
                        return;
                    }

                    row.RedeemedAt = now;
                    row.RedeemedTenantId = tenant.Id;
                }

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) || ex is DbUpdateConcurrencyException)
        {
            return (null, [GenericRegisterFailure]);
        }

        if (inviteLost)
        {
            return (
                null,
                [settings.AllowPublicRegistration ? GenericRegisterFailure : RegistrationDisabledMessage]);
        }

        return (
            new RegisterTenantResponse(tenant.Id, tenant.Slug, admin.Id, admin.Email),
            Array.Empty<string>());
    }

    private async Task<RegistrationInvite?> FindRedeemableInviteAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var hash = InviteCodeHasher.Hash(code);
        var now = DateTimeOffset.UtcNow;
        var invite = await db.RegistrationInvites
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.CodeHash == hash, cancellationToken);
        if (invite is null || invite.RedeemedAt is not null)
        {
            return null;
        }

        if (invite.ExpiresAt is { } expires && expires <= now)
        {
            return null;
        }

        return invite;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return false;
        }

        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }

            if (inner.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
