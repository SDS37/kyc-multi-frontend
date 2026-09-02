namespace Kyc.Api.Application.Identity;

public enum CaptchaPurpose
{
    Login,
    Register
}

public sealed record CaptchaVerification(bool Passed, string? ErrorMessage);

public interface ICaptchaVerifier
{
    Task<CaptchaVerification> VerifyAsync(
        string? token,
        CaptchaPurpose purpose,
        CancellationToken cancellationToken = default);
}
