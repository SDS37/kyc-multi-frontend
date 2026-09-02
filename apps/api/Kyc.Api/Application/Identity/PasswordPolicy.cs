namespace Kyc.Api.Application.Identity;

/// <summary>Shared password length rules for login and register (KYC-109).</summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;
}
