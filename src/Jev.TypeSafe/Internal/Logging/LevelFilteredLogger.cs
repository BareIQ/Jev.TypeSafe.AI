using System;
using Microsoft.Extensions.Logging;

namespace TypeSafe.AI.Internal.Logging;

/// <summary>Enforces the SDK log level regardless of how the underlying logger is configured.</summary>
internal sealed class LevelFilteredLogger : ILogger
{
    private readonly ILogger _inner;
    private readonly LogLevel _minimumLevel;

    public LevelFilteredLogger(ILogger inner, LogLevel minimumLevel)
    {
        _inner = inner;
        _minimumLevel = minimumLevel;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => logLevel != LogLevel.None && _minimumLevel != LogLevel.None && logLevel >= _minimumLevel && _inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            _inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
