using System;
using System.Globalization;

namespace TypeSafe.AI.Internal;

/// <summary>Formats durations as milliseconds, the unit used in messages and logs.</summary>
internal static class Milliseconds
{
    /// <summary>Formats a duration as an invariant number of milliseconds, e.g. <c>10000</c> or <c>0.5</c>.</summary>
    public static string Format(TimeSpan duration) => duration.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Rounds a duration to whole milliseconds for logging.</summary>
    public static long Round(TimeSpan duration) => (long)Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero);
}
