using System.Diagnostics;

namespace Kyc.Api.Infrastructure;

/// <summary>
/// One structured request-complete line per HTTP call (method, path, status, duration).
/// Does not log bodies, query strings, or headers (passwords / JWTs / FormData stay out).
/// Skips <c>/health</c> so liveness probes do not flood stdout.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var skip = HttpMethods.IsGet(context.Request.Method)
                   && context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase);

        var start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            if (!skip)
            {
                var elapsedMs = (int)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                logger.LogInformation(
                    "HTTP {RequestMethod} {RequestPath} {StatusCode} {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    elapsedMs);
            }
        }
    }
}
