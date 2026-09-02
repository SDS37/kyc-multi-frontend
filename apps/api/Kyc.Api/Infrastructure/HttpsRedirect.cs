using Microsoft.AspNetCore.Http;

namespace Kyc.Api.Infrastructure;

/// <summary>
/// When to run <c>UseHttpsRedirection</c> outside Development (issue #108).
/// </summary>
public static class HttpsRedirect
{
    /// <summary>
    /// Skip probes (HTTP liveness/readiness) and requests whose proxy already terminated TLS.
    /// The latter avoids a redirect loop when a proxy already set <c>X-Forwarded-Proto: https</c>
    /// but is not in the forwarded-headers known-proxy list.
    /// </summary>
    public static bool ShouldRedirect(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/ready"))
        {
            return false;
        }

        if (!context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwarded))
        {
            return true;
        }

        var parts = forwarded.ToString().Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
