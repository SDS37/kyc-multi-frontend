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
}
