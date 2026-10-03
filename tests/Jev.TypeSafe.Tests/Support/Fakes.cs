using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Configuration;
using TypeSafe.AI.Internal.Retry;

namespace TypeSafe.AI.Tests.Support;

/// <summary>An in-memory <see cref="IEnvironmentReader"/>.</summary>
internal sealed class FakeEnvironmentReader : IEnvironmentReader
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public FakeEnvironmentReader Set(string name, string? value)
    {
        _values[name] = value;
        return this;
    }

    public string? Get(string name) => _values.TryGetValue(name, out string? value) ? value : null;
}

/// <summary>A random source that always returns the same value.</summary>
internal sealed class FixedRandomSource : IRandomSource
{
    private readonly double _value;

    public FixedRandomSource(double value) => _value = value;

    public double NextDouble() => _value;
}

/// <summary>An <see cref="ILoggerFactory"/> that records the categories it was asked for.</summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    public RecordingLogger Logger { get; } = new();

    public List<string> Categories { get; } = [];

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        Categories.Add(categoryName);
        return Logger;
    }

    public void Dispose()
    {
    }
}

/// <summary>Builds clients that talk to a stub handler with deterministic collaborators.</summary>
internal static class TestClient
{
    public const string ApiKey = "sk-test-1234567890";

    public static Uri BaseUri { get; } = new("https://api.test");

    public static TypeSafeClientOptions Options(Action<TypeSafeClientOptions>? configure = null)
    {
        var options = new TypeSafeClientOptions { ApiKey = ApiKey, BaseUri = BaseUri, LogLevel = TypeSafeLogLevel.Off };
        configure?.Invoke(options);
        return options;
    }

    public static ClientDependencies Dependencies(
        double random = 0,
        FakeEnvironmentReader? environment = null,
        bool isBrowser = false,
        Func<ILogger>? createConsoleLogger = null)
        => new(
            environment ?? new FakeEnvironmentReader(),
            new FixedRandomSource(random),
            isBrowser,
            createConsoleLogger ?? (() => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance));

    public static TypeSafeClient Create(
        System.Net.Http.HttpMessageHandler handler,
        Action<TypeSafeClientOptions>? configure = null,
        Microsoft.Extensions.Time.Testing.FakeTimeProvider? time = null,
        double random = 0)
        => Create(new System.Net.Http.HttpClient(handler), configure, time, random);

    public static TypeSafeClient Create(
        System.Net.Http.HttpClient httpClient,
        Action<TypeSafeClientOptions>? configure = null,
        Microsoft.Extensions.Time.Testing.FakeTimeProvider? time = null,
        double random = 0)
    {
        TypeSafeClientOptions options = Options(configure);
        options.TimeProvider ??= time;
        return new TypeSafeClient(options, httpClient, Dependencies(random));
    }
}
