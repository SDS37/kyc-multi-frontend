using HotChocolate;
using HotChocolate.Execution;

namespace Kyc.Api.Infrastructure;

/// <summary>
/// Logs GraphQL auth failures by error code only — never the query, variables, or token.
/// Logger comes from the current HTTP request (schema DI does not include host <see cref="ILogger{T}"/>).
/// </summary>
public sealed partial class GraphQlAuthErrorLoggingFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        switch (error.Code)
        {
            case "AUTH_NOT_AUTHENTICATED":
            case "AUTH_NOT_AUTHORIZED":
            case "AUTH_FAILED":
                var logger = RequestLogContext.LoggerFactory?
                    .CreateLogger<GraphQlAuthErrorLoggingFilter>();
                if (logger is not null)
                {
                    LogAuthFailure(logger, error.Code);
                }

                break;
        }

        return error;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "GraphQL auth failure {ErrorCode}")]
    private static partial void LogAuthFailure(ILogger logger, string? errorCode);
}
