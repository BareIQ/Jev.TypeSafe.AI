using System;
using System.Collections.Immutable;
using System.Linq;
using TypeSafe.AI.Internal;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>
/// Immutable retry settings. Derive variations with <c>with</c> expressions, for example
/// <c>RetryPolicy.Default with { MaxRetries = 5 }</c>. Invalid values are rejected when set.
/// </summary>
public sealed record RetryPolicy
{
    private static readonly IImmutableSet<int> s_defaultStatusCodes =
        ImmutableHashSet.CreateRange(new[] { 408, 429 }.Concat(Enumerable.Range(500, 100)));

    private readonly int _maxRetries = 2;
    private readonly TimeSpan _initialBackoff = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _maxBackoff = TimeSpan.FromSeconds(5);
    private readonly double _backoffJitter = 0.25;
    private readonly IImmutableSet<int> _retryableStatusCodes = s_defaultStatusCodes;
    private readonly TimeSpan _maxRetryAfter = TimeSpan.FromSeconds(60);

    /// <summary>Gets the SDK default policy: two retries with 500 ms to 5 s exponential backoff.</summary>
    public static RetryPolicy Default { get; } = new();

    /// <summary>Gets a policy that never retries.</summary>
    public static RetryPolicy None { get; } = new() { MaxRetries = 0 };

    /// <summary>Gets the maximum number of retries after the first attempt; <c>0</c> disables retries. Default: 2.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxRetries
    {
        get => _maxRetries;
        init => _maxRetries = value >= 0
            ? value
            : throw OutOfRange(nameof(MaxRetries), $"`retry.maxRetries` must be a non-negative integer, got {value}.");
    }

    /// <summary>Gets the first backoff delay, doubled after each retry up to <see cref="MaxBackoff"/>. Default: 500 ms.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan InitialBackoff
    {
        get => _initialBackoff;
        init => _initialBackoff = NonNegative(value, nameof(InitialBackoff), "retry.backoffInitialMs");
    }

    /// <summary>Gets the maximum backoff delay. Default: 5 s.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan MaxBackoff
    {
        get => _maxBackoff;
        init => _maxBackoff = NonNegative(value, nameof(MaxBackoff), "retry.backoffMaxMs");
    }

    /// <summary>Gets the fraction of each backoff delay randomly subtracted, from 0 to 1. Default: 0.25.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside [0, 1].</exception>
    public double BackoffJitter
    {
        get => _backoffJitter;
        init => _backoffJitter = value is >= 0 and <= 1
            ? value
            : throw OutOfRange(nameof(BackoffJitter), $"`retry.backoffJitter` must be between 0 and 1, got {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
    }

    /// <summary>Gets the HTTP status codes to retry. Default: 408, 429, and 500–599.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A code is outside 100–999.</exception>
    public IImmutableSet<int> RetryableStatusCodes
    {
        get => _retryableStatusCodes;
        init => _retryableStatusCodes = ValidStatusCodes(Guard.NotNull(value));
    }

    /// <summary>Gets a value indicating whether to honor <c>Retry-After</c> and <c>retry-after-ms</c>. Default: <see langword="true"/>.</summary>
    public bool RespectRetryAfter { get; init; } = true;

    /// <summary>Gets the longest server-requested delay to honor; longer delays use backoff instead. Default: 60 s.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan MaxRetryAfter
    {
        get => _maxRetryAfter;
        init => _maxRetryAfter = NonNegative(value, nameof(MaxRetryAfter), "retry.maxRetryAfterMs");
    }

    /// <summary>Gets a value indicating whether to retry <see cref="ApiConnectionException"/>, including interrupted bodies. Default: <see langword="true"/>.</summary>
    public bool RetryOnConnectionError { get; init; } = true;

    /// <summary>Gets a value indicating whether to retry <see cref="ApiTimeoutException"/>. Default: <see langword="true"/>.</summary>
    public bool RetryOnTimeout { get; init; } = true;

    /// <inheritdoc/>
    public bool Equals(RetryPolicy? other)
        => other is not null
            && MaxRetries == other.MaxRetries
            && InitialBackoff == other.InitialBackoff
            && MaxBackoff == other.MaxBackoff
            && BackoffJitter.Equals(other.BackoffJitter)
            && RetryableStatusCodes.SetEquals(other.RetryableStatusCodes)
            && RespectRetryAfter == other.RespectRetryAfter
            && MaxRetryAfter == other.MaxRetryAfter
            && RetryOnConnectionError == other.RetryOnConnectionError
            && RetryOnTimeout == other.RetryOnTimeout;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = MaxRetries;
            hash = (hash * 397) ^ InitialBackoff.GetHashCode();
            hash = (hash * 397) ^ MaxBackoff.GetHashCode();
            hash = (hash * 397) ^ BackoffJitter.GetHashCode();
            hash = (hash * 397) ^ RetryableStatusCodes.Count;
            hash = (hash * 397) ^ MaxRetryAfter.GetHashCode();
            return (hash * 397) ^ ((RespectRetryAfter ? 1 : 0) | (RetryOnConnectionError ? 2 : 0) | (RetryOnTimeout ? 4 : 0));
        }
    }

    private static TimeSpan NonNegative(TimeSpan value, string propertyName, string upstreamName)
        => value >= TimeSpan.Zero
            ? value
            : throw OutOfRange(propertyName, $"`{upstreamName}` must be a non-negative number of milliseconds, got {Milliseconds.Format(value)}.");

    private static IImmutableSet<int> ValidStatusCodes(IImmutableSet<int> codes)
    {
        foreach (int code in codes)
        {
            if (code is < 100 or > 999)
            {
                throw OutOfRange(nameof(RetryableStatusCodes), $"`retry.httpStatuses` must contain HTTP status codes, got {code}.");
            }
        }

        return codes;
    }

    private static ArgumentOutOfRangeException OutOfRange(string propertyName, string message) => new(propertyName, message);

    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("MaxRetries = ").Append(MaxRetries)
            .Append(", InitialBackoff = ").Append(InitialBackoff)
            .Append(", MaxBackoff = ").Append(MaxBackoff)
            .Append(", BackoffJitter = ").Append(BackoffJitter.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(", RetryableStatusCodes = [").Append(string.Join(", ", RetryableStatusCodes.OrderBy(code => code))).Append(']')
            .Append(", RespectRetryAfter = ").Append(RespectRetryAfter)
            .Append(", MaxRetryAfter = ").Append(MaxRetryAfter)
            .Append(", RetryOnConnectionError = ").Append(RetryOnConnectionError)
            .Append(", RetryOnTimeout = ").Append(RetryOnTimeout);
        return true;
    }

    /// <summary>Returns whether this policy retries the failure of the given kind.</summary>
    internal bool ShouldRetry(ApiConnectionException failure) => failure is ApiTimeoutException ? RetryOnTimeout : RetryOnConnectionError;

    /// <summary>Returns whether this policy retries the status code.</summary>
    internal bool ShouldRetry(int statusCode) => RetryableStatusCodes.Contains(statusCode);
}
