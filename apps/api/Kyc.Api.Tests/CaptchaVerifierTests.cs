using System.Net;
using System.Text;
using Kyc.Api.Application.Identity;
using Kyc.Api.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kyc.Api.Tests;

public sealed class CaptchaVerifierTests
{
    [Fact]
    public async Task Turnstile_html_body_fails_closed()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>upstream error</html>", Encoding.UTF8, "text/html")
        };
        var result = await VerifyTurnstileAsync(response);

        Assert.False(result.Passed);
        Assert.Equal(CaptchaMessages.Failed, result.ErrorMessage);
    }

    [Fact]
    public async Task Turnstile_malformed_json_fails_closed()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
        };
        var result = await VerifyTurnstileAsync(response);

        Assert.False(result.Passed);
        Assert.Equal(CaptchaMessages.Failed, result.ErrorMessage);
    }

    private static async Task<CaptchaVerification> VerifyTurnstileAsync(HttpResponseMessage providerResponse)
    {
        using var http = new HttpClient(new StaticHttpHandler(providerResponse))
        {
            BaseAddress = new Uri("https://example.invalid/")
        };
        var options = Options.Create(new CaptchaOptions
        {
            Provider = CaptchaOptions.TurnstileProvider,
            Secret = "test-secret",
            RegisterRequired = true,
            VerifyUrl = "https://example.invalid/turnstile"
        });
        var verifier = new CaptchaVerifier(http, options, NullLogger<CaptchaVerifier>.Instance);
        return await verifier.VerifyAsync("token", CaptchaPurpose.Register);
    }
}

internal sealed class StaticHttpHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(response);
}
