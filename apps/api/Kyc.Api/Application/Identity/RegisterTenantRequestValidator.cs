using System.Net.Mail;
using System.Text.RegularExpressions;
using FluentValidation;

namespace Kyc.Api.Application.Identity;

public sealed partial class RegisterTenantRequestValidator : AbstractValidator<RegisterTenantRequest>
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public RegisterTenantRequestValidator()
    {
        RuleFor(request => request.TenantName)
            .Must(name =>
            {
                var trimmed = name?.Trim() ?? string.Empty;
                return trimmed.Length is >= 2 and <= 200;
            })
            .WithMessage("Tenant name must be between 2 and 200 characters.");

        RuleFor(request => request.TenantSlug)
            .Cascade(CascadeMode.Stop)
            .Must(slug =>
            {
                var normalized = slug?.Trim().ToLowerInvariant() ?? string.Empty;
                return normalized.Length is >= 2 and <= 100;
            })
            .WithMessage("Tenant slug must be between 2 and 100 characters.")
            .Must(slug => SlugPattern().IsMatch(slug?.Trim().ToLowerInvariant() ?? string.Empty))
            .WithMessage("Tenant slug must be lowercase letters, numbers, and hyphens (no leading/trailing hyphen).");

        RuleFor(request => request.AdminEmail)
            .Must(email =>
            {
                var trimmed = email?.Trim() ?? string.Empty;
                return trimmed.Length is > 0 and <= 320 && IsValidEmail(trimmed);
            })
            .WithMessage("A valid admin email is required.");

        RuleFor(request => request.AdminPassword)
            .Cascade(CascadeMode.Stop)
            .Must(password => (password ?? string.Empty).Length >= PasswordPolicy.MinLength)
            .WithMessage($"Password must be at least {PasswordPolicy.MinLength} characters.")
            .Must(password => (password ?? string.Empty).Length <= PasswordPolicy.MaxLength)
            .WithMessage($"Password must be at most {PasswordPolicy.MaxLength} characters.")
            .Must(HasRequiredComplexity)
            .WithMessage("Password must contain upper and lower case letters and at least one digit.");
    }

    private static bool HasRequiredComplexity(string? password)
    {
        var value = password ?? string.Empty;
        return value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit);
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
}
