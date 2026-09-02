namespace Kyc.Api.Application.Identity;

/// <summary>Controls anonymous tenant self-registration (GraphQL + REST).</summary>
public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    /// <summary>
    /// When false, <c>registerTenant</c> is rejected. Default false (fail closed).
    /// Local Development sets true via appsettings.Development.json.
    /// </summary>
    public bool AllowPublicRegistration { get; set; }

    /// <summary>
    /// Break-glass: allow <see cref="AllowPublicRegistration"/> outside Development.
    /// Ignored when <see cref="AllowPublicRegistration"/> is false.
    /// </summary>
    public bool AllowInProduction { get; set; }

    /// <summary>
    /// When set, overrides the environment default (required outside Development).
    /// Open public registration in Development does not require a code unless this is true.
    /// </summary>
    public bool? RequireInviteCode { get; set; }

    /// <summary>Resolved: invite required when public registration is on.</summary>
    public bool InviteRequired { get; set; }

    public void ApplyEnvironment(IHostEnvironment environment)
    {
        InviteRequired = RequireInviteCode ?? !environment.IsDevelopment();
    }
}
