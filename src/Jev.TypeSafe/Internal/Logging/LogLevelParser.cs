using Microsoft.Extensions.Logging;

namespace TypeSafe.AI.Internal.Logging;

/// <summary>Parses and maps <see cref="TypeSafeLogLevel"/> values.</summary>
internal static class LogLevelParser
{
    /// <summary>Parses a lowercase level name, as accepted by <c>TYPESAFE_LOG_LEVEL</c>.</summary>
    /// <param name="value">The level name.</param>
    /// <param name="source">Where the value came from, for the error message.</param>
    /// <exception cref="TypeSafeException">The value is not a level name.</exception>
    public static TypeSafeLogLevel Parse(string value, string source) => value switch
    {
        "debug" => TypeSafeLogLevel.Debug,
        "info" => TypeSafeLogLevel.Info,
        "warn" => TypeSafeLogLevel.Warn,
        "error" => TypeSafeLogLevel.Error,
        "off" => TypeSafeLogLevel.Off,
        _ => throw new TypeSafeException($"Invalid log level \"{value}\" from {source}. Expected one of: debug, info, warn, error, off."),
    };

    /// <summary>Maps an SDK level to the minimum <see cref="LogLevel"/> it enables.</summary>
    public static LogLevel ToMinimumLevel(TypeSafeLogLevel level) => level switch
    {
        TypeSafeLogLevel.Debug => LogLevel.Debug,
        TypeSafeLogLevel.Info => LogLevel.Information,
        TypeSafeLogLevel.Warn => LogLevel.Warning,
        TypeSafeLogLevel.Error => LogLevel.Error,
        _ => LogLevel.None,
    };
}
