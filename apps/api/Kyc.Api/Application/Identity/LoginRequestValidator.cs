using FluentValidation;

namespace Kyc.Api.Application.Identity;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.TenantSlug)
            .Must(slug => !string.IsNullOrWhiteSpace(slug))
            .WithMessage("Tenant slug is required.");

        RuleFor(request => request.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email))
            .WithMessage("Email is required.");

        RuleFor(request => request.Password)
            .Cascade(CascadeMode.Stop)
            .Must(password => !string.IsNullOrWhiteSpace(password))
            .WithMessage("Password is required.")
            .Must(password => (password ?? string.Empty).Length <= PasswordPolicy.MaxLength)
            .WithMessage($"Password must be at most {PasswordPolicy.MaxLength} characters.");
    }
}
