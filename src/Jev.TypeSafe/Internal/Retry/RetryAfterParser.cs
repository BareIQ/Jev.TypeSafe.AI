using System;
using System.Globalization;

namespace TypeSafe.AI.Internal.Retry;

/// <summary>Parses server retry delays from <c>retry-after-ms</c> and <c>Retry-After</c>.</summary>
internal static class RetryAfterParser
{
    private const string RetryAfterMsHeader = "retry-after-ms";
    private const string RetryAfterHeader = "Retry-After";

    /// <summary>Parses the retry delay from response headers.</summary>
    public static TimeSpan? Parse(RawResponse response, DateTimeOffset now)
    {
        string? retryAfterMs = response.TryGetHeader(RetryAfterMsHeader, out string? ms) ? ms : null;
        string? retryAfter = response.TryGetHeader(RetryAfterHeader, out string? value) ? value : null;
        return Parse(retryAfterMs, retryAfter, now);
    }

    /// <summary>
    /// Prefers a valid <c>retry-after-ms</c>; otherwise reads <c>Retry-After</c> as seconds or an HTTP date.
    /// Returns <see langword="null"/> when neither contains a valid delay.
    /// </summary>
    /// <remarks>Blank numeric values count as zero, matching JavaScript's <c>Number("")</c> in the upstream SDK.</remarks>
    public static TimeSpan? Parse(string? retryAfterMs, string? retryAfter, DateTimeOffset now)
    {
        if (retryAfterMs is not null && TryParseNumber(retryAfterMs, out double milliseconds) && milliseconds >= 0)
        {
            return FromMilliseconds(milliseconds);
        }

        if (retryAfter is null)
        {
            return null;
        }

        if (TryParseNumber(retryAfter, out double seconds))
        {
            return seconds >= 0 ? FromMilliseconds(seconds * 1000) : null;
        }

        return DateTimeOffset.TryParse(retryAfter, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date)
            ? Max(TimeSpan.Zero, date - now)
            : null;
    }

    private static bool TryParseNumber(string value, out double number)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            number = 0;
            return true;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && !double.IsNaN(number)
            && !double.IsInfinity(number);
    }

    private static TimeSpan? FromMilliseconds(double milliseconds)
        => milliseconds <= TimeSpan.MaxValue.TotalMilliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null;

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;
}
