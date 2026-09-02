namespace Kyc.Api.Infrastructure;

/// <summary>
/// Response security headers (KYC-091 + issue #108). The API is JSON/GraphQL, not an app shell.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Browsers must not execute API responses as a document. Fetch/XHR from the UIs is unaffected
    /// (the SPA’s own document CSP applies there).
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
}
