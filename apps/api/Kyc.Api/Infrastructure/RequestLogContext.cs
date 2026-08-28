namespace Kyc.Api.Infrastructure;

/// <summary>
/// Per-request <see cref="ILoggerFactory"/> so GraphQL schema services can log
/// without host <c>ILogger{T}</c> in the executor container.
/// </summary>
internal static class RequestLogContext
{
    private static readonly AsyncLocal<ILoggerFactory?> Current = new();

    public static ILoggerFactory? LoggerFactory
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
