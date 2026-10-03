using System;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Http;
using TypeSafe.AI.Internal.Logging;

namespace TypeSafe.AI.Internal.Configuration;

/// <summary>Resolves client options into an immutable configuration: explicit options, then environment variables, then defaults.</summary>
internal static class ClientConfigurationResolver
{
    public const string DefaultBaseUrl = "https://api.typesafe.ai";
    public const string DefaultModel = "jev-latest";
    public const string LoggerCategory = "TypeSafe.AI";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <exception cref="TypeSafeException">A required value is missing, a value is invalid, or the runtime is a browser.</exception>
    public static ClientConfiguration Resolve(TypeSafeClientOptions options, ClientDependencies dependencies)
    {
        if (dependencies.IsBrowser && !options.DangerouslyAllowBrowser)
        {
            throw new TypeSafeException(
                "TypeSafeClient is running in a browser, which would expose your API key to anyone using the page. "
                + "Call the API from a server instead, or set `DangerouslyAllowBrowser` to true if you understand the risk.");
        }

        IEnvironmentReader environment = dependencies.Environment;
        TypeSafeLogLevel logLevel = ResolveLogLevel(options.LogLevel, Read(environment, TypeSafeEnvironmentVariables.LogLevel));
        return new ClientConfiguration
        {
            ApiKey = options.ApiKey ?? Read(environment, TypeSafeEnvironmentVariables.ApiKey) ?? throw MissingApiKey(),
            BaseUrl = ResolveBaseUrl(options.BaseUri, Read(environment, TypeSafeEnvironmentVariables.BaseUrl)),
            DefaultModel = options.DefaultModel ?? Read(environment, TypeSafeEnvironmentVariables.DefaultModel) ?? DefaultModel,
            LogLevel = logLevel,
            Logger = new LevelFilteredLogger(ResolveSink(options, logLevel, dependencies), LogLevelParser.ToMinimumLevel(logLevel)),
            RetryPolicy = options.RetryPolicy ?? RetryPolicy.Default,
            Timeout = OptionsValidator.ValidateTimeout(options.Timeout ?? DefaultTimeout),
            DefaultHeaders = new HeaderMerger().SetAll(options.DefaultHeaders).Build(),
            TimeProvider = options.TimeProvider ?? TimeProvider.System,
        };
    }

    /// <summary>Reads a trimmed variable, treating empty and whitespace-only values as unset.</summary>
    private static string? Read(IEnvironmentReader environment, string name)
    {
        string? value = environment.Get(name)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static TypeSafeException MissingApiKey() => new(
        $"No API key was provided. Pass `ApiKey` to the TypeSafeClient constructor or set the {TypeSafeEnvironmentVariables.ApiKey} environment variable.");

    private static string ResolveBaseUrl(Uri? fromOptions, string? fromEnvironment)
    {
        if (fromOptions is not null)
        {
            return OptionsValidator.ValidateBaseUrl(fromOptions.IsAbsoluteUri ? fromOptions.OriginalString : fromOptions.ToString(), "the `BaseUri` option");
        }

        return fromEnvironment is null
            ? DefaultBaseUrl
            : OptionsValidator.ValidateBaseUrl(fromEnvironment, TypeSafeEnvironmentVariables.BaseUrl);
    }

    private static TypeSafeLogLevel ResolveLogLevel(TypeSafeLogLevel? fromOptions, string? fromEnvironment)
    {
        if (fromOptions is { } level)
        {
            return OptionsValidator.ValidateLogLevel(level);
        }

        return fromEnvironment is null
            ? TypeSafeLogLevel.Warn
            : LogLevelParser.Parse(fromEnvironment, TypeSafeEnvironmentVariables.LogLevel);
    }

    private static ILogger ResolveSink(TypeSafeClientOptions options, TypeSafeLogLevel level, ClientDependencies dependencies)
    {
        if (options.Logger is not null)
        {
            return options.Logger;
        }

        if (options.LoggerFactory is not null)
        {
            return options.LoggerFactory.CreateLogger(LoggerCategory);
        }

        return level == TypeSafeLogLevel.Off ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : dependencies.CreateConsoleLogger();
    }
}
