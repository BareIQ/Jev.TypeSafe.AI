using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace TypeSafe.AI.Tests.Support;

/// <summary>One recorded log call.</summary>
internal sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception)
{
    public override string ToString() => $"{Level}: {Message}";
}

/// <summary>An <see cref="ILogger"/> that records everything and enables every level.</summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly List<LogEntry> _entries = [];
    private readonly object _gate = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public IReadOnlyList<string> Messages => Entries.Select(entry => entry.Message).ToArray();

    public string Text => string.Join("\n", Entries.Select(entry => entry.ToString()));

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
        }
    }
}
