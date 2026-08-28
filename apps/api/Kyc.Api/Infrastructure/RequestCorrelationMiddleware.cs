using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Kyc.Api.Infrastructure;

/// <summary>
/// Accepts a gateway <c>X-Request-Id</c> when it is a safe token; otherwise uses
/// <see cref="HttpContext.TraceIdentifier"/>. Always echoes the id on the response
/// and puts it in the logger scope (KYC-104).
/// </summary>
public sealed class RequestCorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Request-Id";
    public const string LogScopeKey = "RequestId";

    private const int MaxIncomingLength = 128;
    private static readonly Regex SafeRequestId = new(
        @"^[A-Za-z0-9._\-]{1,128}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context, ILogger<RequestCorrelationMiddleware> logger)
    {
        var requestId = ResolveRequestId(context);
        context.TraceIdentifier = requestId;
        RequestLogContext.LoggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        var scopeState = new Dictionary<string, object>
        {
            [LogScopeKey] = requestId
        };
        if (Activity.Current is { } activity)
        {
            scopeState["TraceId"] = activity.TraceId.ToString();
        }

        try
        {
            using (logger.BeginScope(scopeState))
            {
                await next(context);
            }
        }
        finally
        {
            RequestLogContext.LoggerFactory = null;
        }
    }

    internal static string ResolveRequestId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var incoming = values.ToString().Trim();
            if (incoming.Length is > 0 and <= MaxIncomingLength && SafeRequestId.IsMatch(incoming))
            {
                return incoming;
            }
        }

        return context.TraceIdentifier;
    }
}
