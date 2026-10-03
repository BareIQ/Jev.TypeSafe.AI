using System;

namespace TypeSafe.AI.Internal.Configuration;

/// <summary>Validates configuration values, raising the SDK's configuration errors.</summary>
internal static class OptionsValidator
{
    private static readonly TimeSpan s_maxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>Requires a positive, finite timeout the timer APIs accept.</summary>
    /// <exception cref="TypeSafeException">The timeout is invalid.</exception>
    public static TimeSpan ValidateTimeout(TimeSpan timeout)
        => timeout > TimeSpan.Zero && timeout <= s_maxTimeout
            ? timeout
            : throw new TypeSafeException($"`timeout` must be a positive number of milliseconds, got {Milliseconds.Format(timeout)}.");

    /// <summary>Requires an absolute HTTP(S) URL and strips trailing slashes.</summary>
    /// <exception cref="TypeSafeException">The URL is invalid.</exception>
    public static string ValidateBaseUrl(string baseUrl, string source)
    {
        string trimmed = baseUrl.TrimEnd('/');
        bool valid = Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        return valid
            ? trimmed
            : throw new TypeSafeException($"The base URL from {source} must be an absolute http or https URL, got \"{baseUrl}\".");
    }

    /// <summary>Requires a defined log level.</summary>
    /// <exception cref="TypeSafeException">The level is not defined.</exception>
    public static TypeSafeLogLevel ValidateLogLevel(TypeSafeLogLevel level)
        => Enum.IsDefined(typeof(TypeSafeLogLevel), level)
            ? level
            : throw new TypeSafeException(
                $"Invalid log level \"{level}\" from the `LogLevel` option. Expected one of: debug, info, warn, error, off.");
}
