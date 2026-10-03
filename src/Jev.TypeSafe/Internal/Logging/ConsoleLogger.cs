using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace TypeSafe.AI.Internal.Logging;

/// <summary>
/// A minimal console sink used when no logger is configured. Lines are prefixed with
/// <c>[typesafe-sdk]</c>; warnings and errors go to the error stream.
/// </summary>
internal sealed class ConsoleLogger : ILogger
{
    private const string Prefix = "[typesafe-sdk] ";
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly object _gate = new();

    public ConsoleLogger(TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        TextWriter writer = logLevel >= LogLevel.Warning ? _error : _output;
        string message = Prefix + formatter(state, exception);
        lock (_gate)
        {
            writer.WriteLine(message);
            if (exception is not null)
            {
                writer.WriteLine(exception);
            }
        }
    }
}
