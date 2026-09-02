namespace Kyc.Api.Application.Identity;

/// <summary>
/// CAPTCHA gate for anonymous identity (KYC-093).
/// <c>Provider</c>: <c>none</c> (skip unless required — then fail closed), <c>test</c> (bypass token), <c>turnstile</c>.
/// </summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";
    public const string TestProvider = "test";
    public const string TurnstileProvider = "turnstile";
    public const string NoneProvider = "none";

    public string Provider { get; set; } = NoneProvider;
    public bool? RequiredForRegister { get; set; }
    public bool? RequiredForLogin { get; set; }
    public string Secret { get; set; } = string.Empty;
    public string VerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    public string BypassToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;

    public bool RegisterRequired { get; set; }
    public bool LoginRequired { get; set; }

    public void ApplyEnvironment(IHostEnvironment environment)
    {
        RegisterRequired = RequiredForRegister ?? !environment.IsDevelopment();
        LoginRequired = RequiredForLogin ?? false;
        if (TimeoutSeconds <= 0)
        {
            TimeoutSeconds = 5;
        }
    }
}
