using System;
using TypeSafe.AI.Internal.Retry;

namespace TypeSafe.AI;

/// <summary>HTTP 429: the rate limit was exceeded.</summary>
public sealed class RateLimitException : ApiException
{
    /// <summary>Initializes a new instance of the <see cref="RateLimitException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    public RateLimitException(string message, RawResponse response)
        : this(message, response, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RateLimitException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public RateLimitException(string message, RawResponse response, Exception? innerException)
        : this(message, response, innerException, DateTimeOffset.UtcNow)
    {
    }

    internal RateLimitException(string message, RawResponse response, Exception? innerException, DateTimeOffset now)
        : base(message, response, innerException)
        => RetryAfter = RetryAfterParser.Parse(Response, now);

    /// <summary>
    /// Gets the server's requested delay from <c>retry-after-ms</c> or <c>Retry-After</c>, or
    /// <see langword="null"/> when absent or invalid.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
