using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kyc.Api.Application.Identity;
using Microsoft.Extensions.Options;

namespace Kyc.Api.Infrastructure;

public sealed partial class CaptchaVerifier(
    HttpClient http,
    IOptions<CaptchaOptions> options,
    ILogger<CaptchaVerifier> logger) : ICaptchaVerifier
{
    public const string RequiredMessage = CaptchaMessages.Required;
    public const string FailedMessage = CaptchaMessages.Failed;
    public const string NotConfiguredMessage = CaptchaMessages.NotConfigured;

    public async Task<CaptchaVerification> VerifyAsync(
        string? token,
        CaptchaPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var required = purpose == CaptchaPurpose.Register ? settings.RegisterRequired : settings.LoginRequired;
        if (!required)
        {
            return new CaptchaVerification(true, null);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new CaptchaVerification(false, RequiredMessage);
        }

        var provider = settings.Provider.Trim();
        if (provider.Equals(CaptchaOptions.NoneProvider, StringComparison.OrdinalIgnoreCase) ||
            provider.Length == 0)
        {
            return new CaptchaVerification(false, NotConfiguredMessage);
        }

        if (provider.Equals(CaptchaOptions.TestProvider, StringComparison.OrdinalIgnoreCase))
        {
            var ok = !string.IsNullOrWhiteSpace(settings.BypassToken) &&
                     string.Equals(token.Trim(), settings.BypassToken, StringComparison.Ordinal);
            return ok
                ? new CaptchaVerification(true, null)
                : new CaptchaVerification(false, FailedMessage);
        }

        if (!provider.Equals(CaptchaOptions.TurnstileProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new CaptchaVerification(false, NotConfiguredMessage);
        }

        if (string.IsNullOrWhiteSpace(settings.Secret))
        {
            return new CaptchaVerification(false, NotConfiguredMessage);
        }

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = settings.Secret,
                ["response"] = token.Trim()
            });
            using var response = await http.PostAsync(settings.VerifyUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogCaptchaRejected(logger, "http");
                return new CaptchaVerification(false, FailedMessage);
            }

            var payload = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);
            if (payload?.Success is true)
            {
                return new CaptchaVerification(true, null);
            }

            LogCaptchaRejected(logger, "provider");
            return new CaptchaVerification(false, FailedMessage);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogCaptchaRejected(logger, ex.GetType().Name);
            return new CaptchaVerification(false, FailedMessage);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Captcha verification failed {Reason}")]
    private static partial void LogCaptchaRejected(ILogger logger, string reason);

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
