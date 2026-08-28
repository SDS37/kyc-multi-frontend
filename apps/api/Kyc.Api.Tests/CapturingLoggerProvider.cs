using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kyc.Api.Tests;

/// <summary>In-memory logger for asserting KYC-104 log lines without capturing secrets.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentBag<CapturedLog> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string categoryName, ConcurrentBag<CapturedLog> entries) : ILogger
    {
        private static readonly AsyncLocal<Stack<object>> Scopes = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var stack = Scopes.Value ??= new Stack<object>();
            stack.Push(state!);
            return new PopScope(stack);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = Scopes.Value is { Count: > 0 }
                ? Scopes.Value.ToArray()
                : [];
            entries.Add(new CapturedLog(logLevel, categoryName, formatter(state, exception), scopes));
        }

        private sealed class PopScope(Stack<object> stack) : IDisposable
        {
            public void Dispose()
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
        }
    }
}

public sealed record CapturedLog(
    LogLevel Level,
    string Category,
    string Message,
    object[] Scopes);
