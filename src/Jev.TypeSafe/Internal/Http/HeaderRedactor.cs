using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TypeSafe.AI.Internal.Http;

/// <summary>Masks credential header values for logging.</summary>
internal static class HeaderRedactor
{
    private const string Mask = "***";
    private const int MinimumSecretLengthForTail = 9;
    private const int TailLength = 4;

    /// <summary>Headers whose values keep their scheme and, for long secrets, the last four characters.</summary>
    private static readonly HashSet<string> s_keyHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "proxy-authorization",
        "x-api-key",
    };

    /// <summary>Headers whose values are masked completely.</summary>
    private static readonly HashSet<string> s_opaqueHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "cookie",
        "set-cookie",
    };

    private static readonly Regex s_whitespace = new(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Returns a copy with credential values masked; the input is not modified.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Redact(IEnumerable<KeyValuePair<string, string>> headers)
        => [.. headers.Select(header => new KeyValuePair<string, string>(header.Key, Redact(header.Key, header.Value)))];

    /// <summary>Masks one header value.</summary>
    public static string Redact(string name, string value)
    {
        if (s_keyHeaders.Contains(name))
        {
            return RedactKey(value);
        }

        return s_opaqueHeaders.Contains(name) ? Mask : value;
    }

    private static string RedactKey(string value)
    {
        string scheme = string.Empty;
        string secret = value;
        if (value.IndexOf(' ') >= 0)
        {
            string[] parts = s_whitespace.Split(value);
            scheme = parts[0];
            secret = parts.Length > 1 ? parts[1] : string.Empty;
        }

        string tail = secret.Length >= MinimumSecretLengthForTail ? secret.Substring(secret.Length - TailLength) : string.Empty;
        return (scheme.Length > 0 ? scheme + " " : string.Empty) + Mask + tail;
    }
}
