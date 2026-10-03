using System;

namespace TypeSafe.AI.Internal.Retry;

/// <summary>Computes retry delays.</summary>
internal static class RetryMath
{
    /// <summary>The largest delay the timer APIs accept.</summary>
    private const double MaxDelayMs = int.MaxValue;

    /// <summary>Doubling stops here; beyond it <c>2^attempt</c> overflows to infinity, and <c>0 * infinity</c> is NaN.</summary>
    private const int MaxDoublings = 1000;

    /// <summary>
    /// Uses an allowed server delay exactly; otherwise capped exponential backoff with up to
    /// <see cref="RetryPolicy.BackoffJitter"/> randomly subtracted.
    /// </summary>
    /// <param name="attempt">The zero-based attempt that failed.</param>
    /// <param name="retryAfter">The server's requested delay, if any.</param>
    /// <param name="policy">The retry policy.</param>
    /// <param name="random">A random number in [0, 1).</param>
    public static TimeSpan ComputeDelay(int attempt, TimeSpan? retryAfter, RetryPolicy policy, double random)
    {
        if (policy.RespectRetryAfter && retryAfter is { } serverDelay && serverDelay <= policy.MaxRetryAfter)
        {
            return serverDelay;
        }

        double exponential = Math.Min(policy.InitialBackoff.TotalMilliseconds * Math.Pow(2, Math.Min(attempt, MaxDoublings)), policy.MaxBackoff.TotalMilliseconds);
        double jittered = Math.Round(exponential * (1 - (random * policy.BackoffJitter)), MidpointRounding.AwayFromZero);
        return TimeSpan.FromMilliseconds(Math.Min(jittered, MaxDelayMs));
    }
}
