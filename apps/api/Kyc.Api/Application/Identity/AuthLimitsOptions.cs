namespace Kyc.Api.Application.Identity;

/// <summary>
/// Fixed-window permits per client IP (KYC-093). Zero in config means “use environment default”.
/// </summary>
public sealed class AuthLimitsOptions
{
    public const string SectionName = "AuthLimits";

    public int LoginPermitPerMinute { get; set; }
    public int RegisterPermitPerMinute { get; set; }
    public int GraphqlPermitPerMinute { get; set; }

    public static AuthLimitsOptions Bind(IConfiguration configuration, IHostEnvironment environment)
    {
        var defaults = environment.IsDevelopment() ? DevelopmentDefaults() : ProductionDefaults();
        var configured = configuration.GetSection(SectionName).Get<AuthLimitsOptions>() ?? new AuthLimitsOptions();
        return new AuthLimitsOptions
        {
            LoginPermitPerMinute = PositiveOrDefault(
                configured.LoginPermitPerMinute,
                defaults.LoginPermitPerMinute),
            RegisterPermitPerMinute = PositiveOrDefault(
                configured.RegisterPermitPerMinute,
                defaults.RegisterPermitPerMinute),
            GraphqlPermitPerMinute = PositiveOrDefault(
                configured.GraphqlPermitPerMinute,
                defaults.GraphqlPermitPerMinute)
        };
    }

    public void Validate()
    {
        if (LoginPermitPerMinute <= 0)
        {
            throw new InvalidOperationException("AuthLimits:LoginPermitPerMinute must be greater than 0.");
        }

        if (RegisterPermitPerMinute <= 0)
        {
            throw new InvalidOperationException("AuthLimits:RegisterPermitPerMinute must be greater than 0.");
        }

        if (GraphqlPermitPerMinute <= 0)
        {
            throw new InvalidOperationException("AuthLimits:GraphqlPermitPerMinute must be greater than 0.");
        }
    }

    public static AuthLimitsOptions DevelopmentDefaults() => new()
    {
        LoginPermitPerMinute = 120,
        RegisterPermitPerMinute = 30,
        GraphqlPermitPerMinute = 120
    };

    public static AuthLimitsOptions ProductionDefaults() => new()
    {
        LoginPermitPerMinute = 10,
        RegisterPermitPerMinute = 3,
        GraphqlPermitPerMinute = 60
    };

    private static int PositiveOrDefault(int configured, int fallback) =>
        configured > 0 ? configured : fallback;
}
