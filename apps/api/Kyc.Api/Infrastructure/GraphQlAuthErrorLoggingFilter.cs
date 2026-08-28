using HotChocolate;
using HotChocolate.Execution;

namespace Kyc.Api.Infrastructure;

/// <summary>
/// Logs GraphQL auth failures by error code only — never the query, variables, or token.
/// Logger comes from the current HTTP request (schema DI does not include host <see cref="ILogger{T}"/>).
/// </summary>
public sealed class GraphQlAuthErrorLoggingFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        switch (error.Code)
        {
            case "AUTH_NOT_AUTHENTICATED":
            case "AUTH_NOT_AUTHORIZED":
            case "AUTH_FAILED":
                RequestLogContext.LoggerFactory?
                    .CreateLogger<GraphQlAuthErrorLoggingFilter>()
                    .LogWarning("GraphQL auth failure {ErrorCode}", error.Code);
                break;
        }

        return error;
    }
}
